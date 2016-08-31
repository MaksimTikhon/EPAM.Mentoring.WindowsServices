using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FileProcessingService
{
	public class FileService
	{
		private string inDirectory;
		private string outDirectory;
		private string tempDirectory;

		private FileSystemWatcher watcher;
		private Task workTask;
		private CancellationTokenSource tokenSource;
		private AutoResetEvent newFileEvent;

		private Document document;
		private Section section;
		private PdfDocumentRenderer pdfRender;

		public FileService(string inDir, string outDir, string tempDir)
		{
			inDirectory = inDir;
			outDirectory = outDir;
			tempDirectory = tempDir;

			if (!Directory.Exists(inDirectory))
			{
				Directory.CreateDirectory(inDirectory);
			}

			if (!Directory.Exists(outDirectory))
			{
				Directory.CreateDirectory(outDirectory);
			}

			if (!Directory.Exists(tempDirectory))
			{
				Directory.CreateDirectory(tempDirectory);
			}

			watcher = new FileSystemWatcher(inDirectory);
			watcher.Created += Watcher_Created;

			tokenSource = new CancellationTokenSource();
			workTask = new Task(() => WorkProcedure(tokenSource.Token));
			newFileEvent = new AutoResetEvent(false);
		}

		public void WorkProcedure(CancellationToken token)
		{
			var currentImageIndex = -1;
			var imageCount = 0;
			var nextPageWaiting = false;
			CreateNewDocument();

			do
			{
				foreach (var file in Directory.EnumerateFiles(inDirectory).Skip(imageCount))
				{
					var fileName = Path.GetFileName(file);
					if (IsValidFormat(fileName))
					{
						var imageIndex = GetIndex(fileName);
						if (imageIndex != currentImageIndex + 1 && currentImageIndex != -1 && nextPageWaiting)
						{
							SaveDocument();
							CreateNewDocument();
							nextPageWaiting = false;
						}

						if (TryOpen(file, 3))
						{
							AddImageToDocument(file);
							imageCount++;
							currentImageIndex = imageIndex;
							nextPageWaiting = true;
						}
					}
					else
					{
						var outFile = Path.Combine(tempDirectory, fileName);
						if (TryOpen(file, 3))
						{
							if (File.Exists(outFile))
							{
								File.Delete(file);
							}
							else
							{
								File.Move(file, outFile);
							}
						}
					}
				}
				
				if (!newFileEvent.WaitOne(5000) && nextPageWaiting)
				{
					SaveDocument();
					CreateNewDocument();
					nextPageWaiting = false;
				}

				if(token.IsCancellationRequested)
				{
					if(nextPageWaiting)
					{
						SaveDocument();
					}

					foreach (var file in Directory.EnumerateFiles(inDirectory))
					{
						if (TryOpen(file, 3))
						{
							File.Delete(file);
						}
					}
				}
			}
			while (!token.IsCancellationRequested);
		}

		private void CreateNewDocument()
		{
			document = new Document();
			section = document.AddSection();
			pdfRender = new PdfDocumentRenderer();
		}

		private void SaveDocument()
		{
			var documentIndex = Directory.GetFiles(outDirectory).Length + 1;
			var resultFile = Path.Combine(outDirectory, string.Format("result_{0}.pdf", documentIndex));

			pdfRender.Document = document;
			pdfRender.RenderDocument();	
			pdfRender.Save(resultFile);
		}

		private void AddImageToDocument(string file)
		{
			var image = section.AddImage(file);

			image.Height = document.DefaultPageSetup.PageHeight;
			image.Width = document.DefaultPageSetup.PageWidth;
			image.ScaleHeight = 0.75;
			image.ScaleWidth = 0.75;

			section.AddPageBreak();
		}

		private bool IsValidFormat(string fileName)
		{
			return Regex.IsMatch(fileName, @"^img_[0-9]{3}.(jpg|png|jpeg)$");
		}

		private int GetIndex(string fileName)
		{
			var match = Regex.Match(fileName, @"[0-9]{3}");

			return match.Success ? int.Parse(match.Value) : -1;
		}

		private void Watcher_Created(object sender, FileSystemEventArgs e)
		{
			newFileEvent.Set();
		}

		public void Start()
		{
			workTask.Start();
			watcher.EnableRaisingEvents = true;
		}

		public void Stop()
		{
			watcher.EnableRaisingEvents = false;
			tokenSource.Cancel();
			workTask.Wait();
		}

		private bool TryOpen(string fileName, int tryCount)
		{
			for (int i = 0; i < tryCount; i++)
			{
				try
				{
					var file = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.None);
					file.Close();

					return true;
				}
				catch (IOException)
				{
					Thread.Sleep(5000);
				}
			}

			return false;
		}
	}
}

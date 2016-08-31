using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.IO;
using Topshelf;

namespace FileProcessingService
{
	class Program
	{
		static void Main(string[] args)
		{
			var currentDir = AppDomain.CurrentDomain.BaseDirectory;
			var inDir = Path.Combine(currentDir, "in");
			var outDir = Path.Combine(currentDir, "out");
			var tempDir = Path.Combine(currentDir, "temp");

			var loggingConfiguration = new LoggingConfiguration();
			var fileTarget = new FileTarget()
			{
				Name = "Default",
				FileName = Path.Combine(currentDir, "log.txt"),
				Layout = "${date} ${message} ${onexception:inner=${exception:format=toString}}"
			};

			loggingConfiguration.AddTarget(fileTarget);
			loggingConfiguration.AddRuleForAllLevels(fileTarget);

			var logFactory = new LogFactory(loggingConfiguration);

			HostFactory.Run(
				hostConf =>
				{
					hostConf.Service<FileService>(
						s =>
						{
							s.ConstructUsing(() => new FileService(inDir, outDir, tempDir));
							s.WhenStarted(serv => serv.Start());
							s.WhenStopped(serv => serv.Stop());
						}).UseNLog(logFactory);
					hostConf.SetServiceName("MyFileService");
					hostConf.SetDisplayName("My File Service");
					hostConf.StartAutomaticallyDelayed();
					hostConf.RunAsLocalService();
				});
		}
	}
}

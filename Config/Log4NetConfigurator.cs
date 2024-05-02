using log4net;
using log4net.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Sigma.Tool.UPS.DiagnosticData.Config
{
    internal class Log4NetConfigurator
    {
        public static void configure(string LogFilePath)
        {
            // Initialize log4net (read information from App.config)
            XmlConfigurator.Configure();
            var repository = LogManager.GetRepository() as log4net.Repository.Hierarchy.Hierarchy;
            var appenders = repository.GetAppenders();
            foreach (var appender in appenders)
            {
                // Check if the appender is of type FileAppender
                if (appender is log4net.Appender.FileAppender fileAppender)
                {
                    // Update the file path for the FileAppender
                    fileAppender.File = LogFilePath;
                    fileAppender.ActivateOptions(); // apply changes
                }
            }
        }
    }
}

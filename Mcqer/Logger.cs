using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mcqer
{
	internal class Logger : ILogger
	{
		private string _logPath;
		public Logger(string logPath)
		{
			_logPath = logPath;
		}
		public void Log(string msg)
		{
			string logMsg = $"[{DateTime.Now}] - {msg}\n";
			File.AppendAllText(_logPath, logMsg);
		}
		public void Log(Exception exception)
		{
			Log($"{exception}");
		}


	}
}

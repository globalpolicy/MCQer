using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mcqer
{
	internal interface ILogger
	{
		void Log(string message);
		void Log(Exception exception);
	}
}

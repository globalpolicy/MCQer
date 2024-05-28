﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mcqer
{
	internal struct Question
	{
		internal string QuestionText { get; set; }
		internal string Option1 { get; set; }
		internal string Option2 { get; set; }
		internal string Option3 { get; set; }
		internal string Option4 { get; set; }
		internal string Option5 { get; set; }
		internal int CorrectOptionNumber { get; set; } // 1-indexed
		internal bool HasImages { get; set; } // whether the options are base64-encoded images
		internal string Category { get; set; }
		internal string Source { get; set; }
	}
}

using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mcqer
{
	internal class SQLiteWriter : IQuestionWriter
	{
		private readonly string _filePath;
		private SqliteConnection _dbConnection;
		private ILogger _logger;
		internal SQLiteWriter(string filePath, ILogger logger)
		{
			_filePath = filePath;
			_dbConnection = new SqliteConnection($"Data Source={_filePath}");
			_logger = logger;
			InitializeDB();
		}

		private void InitializeDB()
		{
			_dbConnection.Open();

			var command = _dbConnection.CreateCommand();
			command.CommandText = @"CREATE TABLE IF NOT EXISTS questions 
					(question_text TEXT, option1 TEXT, option2 TEXT, option3 TEXT, option4 TEXT, option5 TEXT, 
					correct_option_number INTEGER, has_images INTEGER, category TEXT, source TEXT, 
					UNIQUE(question_text,option1,option2,option3,option4,option5))";

			command.ExecuteNonQuery();

		}

		public bool WriteQuestion(Question question)
		{
			bool retval = false;
			try
			{
				var command = _dbConnection.CreateCommand();
				command.CommandText = @"INSERT INTO questions VALUES
					($question_text, $option1, $option2, $option3, $option4, $option5, 
					$correct_option_number, $has_images, $category, $source)";
				command.Parameters.AddWithValue("$question_text", question.QuestionText);
				command.Parameters.AddWithValue("$option1", question.Option1);
				command.Parameters.AddWithValue("$option2", question.Option2);
				command.Parameters.AddWithValue("$option3", question.Option3);
				command.Parameters.AddWithValue("$option4", question.Option4);
				command.Parameters.AddWithValue("$option5", question.Option5);
				command.Parameters.AddWithValue("$correct_option_number", question.CorrectOptionNumber);
				command.Parameters.AddWithValue("$has_images", question.HasImages);
				command.Parameters.AddWithValue("$category", question.Category);
				command.Parameters.AddWithValue("$source", question.Source);

				command.ExecuteNonQuery();
				retval = true;
			}
			catch (Exception ex)
			{
				_logger.Log($"Exception occurred when writing {question} to DB: {ex}");
			}
			return retval;


		}
	}
}

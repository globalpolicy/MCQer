using Microsoft.Data.Sqlite;
using System.Text;

namespace Ankier
{
	public class FlashCard
	{
		private string _dbPath;
		private SqliteConnection _dbConnection;

		public delegate void NotificationHandler(string msg);
		private event NotificationHandler _notificationOccurred;
		public event NotificationHandler OnNotification
		{
			add
			{
				_notificationOccurred += value;
			}
			remove
			{
				_notificationOccurred -= value;
			}
		}
		public FlashCard(string dbPath)
		{
			_dbPath = dbPath;
			_dbConnection = new SqliteConnection($"Data Source={dbPath}");
			_dbConnection.Open();
		}

		/// <summary>
		/// Creates Anki flashcard files for each category in the DB. A typical use-case - create a new deck for each category and import the corresponding file in Anki
		/// </summary>
		/// <param name="outputDirectory"></param>
		public async Task MakeAnkiFlashCards(string outputDirectory)
		{
			var categories = await GetDistinctCategories();
			foreach (string category in categories)
			{
				var flashCardForCategory = (await GetFlashCardForCategory(category)).ToString();
				File.AppendAllText(Path.Combine(outputDirectory, category + ".txt"), flashCardForCategory);
				Notify($"Completed creating flashcards for {category}");
			}
		}

		private async Task<List<string>> GetDistinctCategories()
		{
			List<string> retval = new List<string>();

			var command = _dbConnection.CreateCommand();

			command.CommandText = @"SELECT DISTINCT category FROM questions";

			var reader = await command.ExecuteReaderAsync();

			while (reader.Read())
			{
				string? category = reader["category"] as string;
				if (category != null)
				{
					retval.Add(category);
				}
			}

			reader.Close();
			return retval;
		}

		private async Task<StringBuilder> GetFlashCardForCategory(string category)
		{
			var sb = new StringBuilder();

			var command = _dbConnection.CreateCommand();

			command.CommandText = @"SELECT * FROM questions WHERE category=$category_value";
			command.Parameters.AddWithValue("$category_value", category);

			var reader = await command.ExecuteReaderAsync();
			sb.AppendLine("#separator:tab");
			sb.AppendLine("#html:true");
			sb.AppendLine("");

			while (reader.Read())
			{
				try
				{
					string? questionText = reader["question_text"] as string;
					string? option1 = reader["option1"] as string;
					string? option2 = reader["option2"] as string;
					string? option3 = reader["option3"] as string;
					string? option4 = reader["option4"] as string;
					string? option5 = reader["option5"] as string;
					long? correctOptionNumber = reader["correct_option_number"] as long?;
					bool? hasImages = reader["has_images"] as long? == 1;

					if (string.IsNullOrEmpty(questionText))
					{
						Notify("Found empty or null question entry! Skipping..");
						continue;
					}

					if (correctOptionNumber == null)
					{
						Notify("Found empty correct_option_number field! Skipping..");
						continue;
					}

					#region Creation of each flash card entity

					// strip newlines and tabs from question text and options.
					// this is avoid multi-line strings so as to not have to provide double or triple quotes for escaping actual quotes in text
					// and to avoid collision with Anki's tab-based delimeter between flash card entities
					string questionTextClean = (questionText ?? "").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
					string option1Clean = (option1 ?? "").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
					string option2Clean = (option2 ?? "").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
					string option3Clean = (option3 ?? "").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
					string option4Clean = (option4 ?? "").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
					string option5Clean = (option5 ?? "").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");

					StringBuilder flashCardEntity = new StringBuilder();

					// construct the front-side of the card
					flashCardEntity.Append($"{questionTextClean}");
					flashCardEntity.Append($"<br>");
					flashCardEntity.Append($"<ol type=\"a\">");
					flashCardEntity.Append($"<li>{option1Clean}</li>");
					flashCardEntity.Append($"<li>{option2Clean}</li>");

					if (!string.IsNullOrEmpty(option3Clean))
						flashCardEntity.Append($"<li>{option3Clean}</li>");
					if (!string.IsNullOrEmpty(option4Clean))
						flashCardEntity.Append($"<li>{option4Clean}</li>");
					if (!string.IsNullOrEmpty(option5Clean))
						flashCardEntity.Append($"<li>{option5Clean}</li>");

					flashCardEntity.Append("</ol>");

					// construct the back-side of the card
					flashCardEntity.Append("\t");

					string correctAnswer = "";
					switch (correctOptionNumber)
					{
						case 1:
							correctAnswer = $"a. {option1Clean}";
							break;
						case 2:
							correctAnswer = $"b. {option2Clean}";
							break;
						case 3:
							correctAnswer = $"c. {option3Clean}";
							break;
						case 4:
							correctAnswer = $"d. {option4Clean}";
							break;
						case 5:
							correctAnswer = $"e. {option5Clean}";
							break;
					}
					flashCardEntity.Append(correctAnswer);

					// construct eof for this flash card entity
					flashCardEntity.Append("\n");

					#endregion

					// concatenate the constructed flash card entity
					sb.Append(flashCardEntity);
				}
				catch (Exception ex)
				{
					Notify(ex.ToString());
				}
			}

			reader.Close();


			return sb;
		}

		private string GenerateImgTagIfNeeded(string optionText, bool questionHasImages)
		{
			string retval = optionText;

			if (questionHasImages)
				retval = $"<img src=\"data:image/png;base64,{optionText}\">";

			return retval;
		}

		private void Notify(string msg)
		{
			if (_notificationOccurred != null)
			{
				_notificationOccurred(msg);
			}
		}
	}
}

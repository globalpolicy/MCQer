using Ankier;

namespace Mcqer
{
	internal class Program
	{
		private const string DB_PATH = @"mcqer-output.db";
		private const string LOG_PATH = @"mcqer.log";
		private static Logger logger = new Logger(LOG_PATH);
		static async Task Main(string[] args)
		{
			if (args.Contains("scrape"))
			{
				LogMsg("Scraper started.");
				using (HttpClient httpClient = new HttpClient())
				{
					Requester requester = Requester.GetRequester(new HttpClient());
					IQuestionWriter questionWriter = new SQLiteWriter(DB_PATH, logger);
					IndiabixScraper indiabixScraper = new IndiabixScraper(requester, questionWriter);
					indiabixScraper.OnProgressOccurred += IndiabixScraper_OnProgressOccurred;
					await indiabixScraper.Scrape();
					LogMsg("Scraping completed.");
				}
			}

			if (args.Contains("anki")) // create Anki flashcard files
			{
				LogMsg("Creating Anki flashcards.");
				FlashCard ankier = new FlashCard(DB_PATH);
				ankier.OnNotification += Ankier_OnNotification;
				await ankier.MakeAnkiFlashCards(Environment.CurrentDirectory);
				LogMsg("Flashcards creation completed.");
			}


		}

		private static void Ankier_OnNotification(string msg)
		{
			LogMsg(msg);
		}

		private static void IndiabixScraper_OnProgressOccurred(string msg)
		{
			LogMsg(msg);
		}

		private static void LogMsg(string msg)
		{
			Console.WriteLine(msg);
			logger.Log(msg);
		}
	}
}

namespace Mcqer
{
	internal class Program
	{
		private const string DB_PATH = @"mcqer-output.db";
		private const string LOG_PATH = @"mcqer.log";
		private static Logger logger = new Logger(LOG_PATH);
		static async Task Main(string[] args)
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

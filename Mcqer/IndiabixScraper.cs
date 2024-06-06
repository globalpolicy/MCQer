using HtmlAgilityPack;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Mcqer
{
	internal class IndiabixScraper
	{
		private const string _sourceName = "Indiabix";
		private const string _rootUrl = "https://www.indiabix.com";
		private const string _baseUrl = "https://www.indiabix.com/civil-engineering";

		private readonly HashSet<string> _categories = new() { "building-materials", "surveying", "building-construction", "concrete-technology",
			"soil-mechanics-and-foundation-engineering", "advanced-surveying", "strength-of-materials", "rcc-structures-design",
			"steel-structure-design", "construction-management", "theory-of-structures", "structural-design-specifications",
			"estimating-and-costing", "tunnelling", "engineering-economy", "upsc-civil-service-exam-questions" };

		private readonly Requester _requester;
		private readonly IQuestionWriter _questionWriter;

		internal delegate void ProgressOccurredHandler(string msg);
		private event ProgressOccurredHandler _progressOccurred;
		internal event ProgressOccurredHandler OnProgressOccurred
		{
			add
			{
				_progressOccurred += value;
			}
			remove
			{
				_progressOccurred -= value;
			}
		}


		internal IndiabixScraper(Requester requester, IQuestionWriter questionWriter)
		{
			_requester = requester;
			_questionWriter = questionWriter;
		}

		internal async Task Scrape()
		{
			foreach (string category in _categories)
			{
				// get the contents of the root page for this category
				string categoryUrl = $"{_baseUrl}/{category}";

				// ready a list of all URLs in the category
				List<string> sectionUrls = new List<string>() { categoryUrl };
				sectionUrls.AddRange(await FindAllSectionUrlsFromCategoryUrl(categoryUrl));

				// iterate over each section in the category
				foreach (string sectionUrl in sectionUrls)
				{
					Report($" Processing section URL for {category} - {sectionUrl}");

					List<string> pageUrls = new List<string>() { sectionUrl };

					pageUrls.AddRange(await FindAllPageUrlsFromSectionUrl(sectionUrl));

					// iterate over each page in the section
					foreach (string pageUrl in pageUrls)
					{
						HttpResponseMessage pageResponse = await _requester.GetRequest(pageUrl);
						if (pageResponse.IsSuccessStatusCode)
						{
							string pageContent = pageResponse.Content.ReadAsStringAsync().Result ?? "";
							if (string.IsNullOrEmpty(pageContent))
								continue;

							ICollection<Question> questionsInpage = ExtractQuestionsFromPage(pageContent, category);
							int recordsWritten = 0;
							foreach (var item in questionsInpage)
							{
								if (_questionWriter.WriteQuestion(item))
									recordsWritten++;
							}
							if (recordsWritten > 0)
								Report($"   Wrote {recordsWritten} questions to DB");
						}
					}

				}


			}
		}

		private async Task<List<string>> FindAllSectionUrlsFromCategoryUrl(string categoryUrl)
		{
			List<string> retval = new List<string>();

			HttpResponseMessage pageResponse = await _requester.GetRequest(categoryUrl);
			if (pageResponse.IsSuccessStatusCode)
			{
				string pageContent = pageResponse.Content.ReadAsStringAsync().Result ?? "";
				if (!string.IsNullOrEmpty(pageContent))
				{
					Report($"Found section URL for category {categoryUrl}");
					string regex = @$"<a href=""({Regex.Escape(categoryUrl)}\/\d+)"">";
					MatchCollection matches = Regex.Matches(pageContent, regex);
					foreach (Match match in matches)
					{
						if (match.Groups.Count == 2)
							retval.Add(match.Groups[1].Value);
						Report($"Found section URL - {match.Groups[1].Value}");
					}
				}
			}

			return retval;
		}

		private async Task<List<string>> FindAllPageUrlsFromSectionUrl(string sectionUrl)
		{
			List<string> retval = new List<string>();

			HttpResponseMessage pageResponse = await _requester.GetRequest(sectionUrl);
			if (pageResponse.IsSuccessStatusCode)
			{
				string pageContent = pageResponse.Content.ReadAsStringAsync().Result ?? "";
				if (!string.IsNullOrEmpty(pageContent))
				{
					HtmlDocument htmlDocument = new HtmlDocument();
					htmlDocument.LoadHtml(pageContent);

					HtmlNode pageUrlTemplateNode = htmlDocument.DocumentNode.SelectSingleNode("//input[contains(@id,'inp_pg_no_url')]");
					if (pageUrlTemplateNode != null)
					{
						string pageUrlTemplate = pageUrlTemplateNode.GetAttributeValue("value", "");
						if (!string.IsNullOrEmpty(pageUrlTemplate))
						{
							HtmlNode maxPageNumberNode = htmlDocument.DocumentNode.SelectSingleNode("//input[contains(@id,'inp_pg_no_max')]");
							int maxPageNumber = maxPageNumberNode.GetAttributeValue("value", 1);

							Report($" Found page URL {sectionUrl}");
							for (int i = 1; i <= maxPageNumber; i++)
							{
								string fixedWidthPageNumber = i.ToString("000");
								string pageUrl = pageUrlTemplate.Replace("[[[p-no]]]", fixedWidthPageNumber);
								retval.Add(pageUrl);
								Report($" Found page URL - {pageUrl}");

							}
						}
					}
					else
					{
						retval.AddRange(await GetPageUrlsIteratively(pageContent));
					}

				}
			}




			return retval;
		}

		private async Task<List<string>> GetPageUrlsIteratively(string firstPageContent)
		{
			var retval = new List<string>();

			string newPageContent = firstPageContent;
			HtmlDocument htmlDocument = new HtmlDocument();

			do
			{
				htmlDocument.LoadHtml(newPageContent);
				newPageContent = ""; // reset the loop variable
				HtmlNodeCollection pageItemNodes = htmlDocument.DocumentNode.SelectNodes("//li[contains(@class,'page-item')]");
				if (pageItemNodes != null) // some sections (eg. https://www.indiabix.com/civil-engineering/building-construction/014001) don't have a pagination control at all
				{
					foreach (HtmlNode pageItemNode in pageItemNodes)
					{
						if (pageItemNode.InnerHtml.Contains("Next") && pageItemNode.InnerHtml.Contains("</span>")) // drill down to the required node
						{
							HtmlNode correctNode = pageItemNode.SelectSingleNode(".//a[contains(@class,'page-link')]");
							string newPageLink = correctNode.GetAttributeValue("href", "");
							if (newPageLink != "" && newPageLink != "#")
							{
								retval.Add(newPageLink);
								newPageContent = (await _requester.GetRequest(newPageLink)).Content.ReadAsStringAsync().Result;
							}

							break;
						}
					}
				}

			}
			while (!string.IsNullOrEmpty(newPageContent));


			return retval;
		}

		private ICollection<Question> ExtractQuestionsFromPage(string pageContent, string pageCategory)
		{
			List<Question> questions = new List<Question>();

			HtmlDocument htmlDocument = new HtmlDocument();
			htmlDocument.LoadHtml(pageContent);

			HtmlNodeCollection questionRootNodes = htmlDocument.DocumentNode.SelectNodes(".//div[contains(@class, 'bix-div-container')]");
			foreach (HtmlNode questionRootNode in questionRootNodes)
			{
				Question question = new Question();
				question.Source = _sourceName;
				question.Category = pageCategory;

				HtmlNode questionNode = questionRootNode.SelectSingleNode(".//div[contains(@class, 'bix-td-qtxt')]");
				question.QuestionText = questionNode.InnerHtml;

				List<string> options = new List<string>();
				HtmlNode optionsRootNode = questionRootNode.SelectSingleNode(".//div[contains(@class, 'bix-tbl-options')]");
				HtmlNodeCollection optionNodes = optionsRootNode.SelectNodes(".//div[contains(@class,'flex-wrap')]");
				foreach (HtmlNode optionNode in optionNodes)
					options.Add(optionNode.InnerHtml);

				question.Option1 = options[0];
				question.Option2 = options[1];
				if (options.Count > 2)
					question.Option3 = options[2];
				if (options.Count > 3)
					question.Option4 = options[3];
				if (options.Count > 4)
					question.Option5 = options[4];

				HtmlNode answerNode = questionRootNode.SelectSingleNode(".//input[contains(@class,'jq-hdnakq')]");
				question.CorrectOptionNumber = GetIndexFromAlphabetOption(answerNode.GetAttributeValue("value", ""));

				// download option image if any option is an image link
				NormalizeImages(ref question);

				questions.Add(question);
			}

			return questions;
		}

		/// <summary>
		/// Checks if the given Question has any <img> tag in its question or options. If it does, retrieve the image and use its base64 encoded version in place instead
		/// </summary>
		/// <param name="question"></param>
		private void NormalizeImages(ref Question question)
		{
			question.QuestionText = NormalizeImage(question.QuestionText, out bool isImage0);
			question.Option1 = NormalizeImage(question.Option1, out bool isImage1);
			question.Option2 = NormalizeImage(question.Option2, out bool isImage2);
			question.Option3 = NormalizeImage(question.Option3, out bool isImage3);
			question.Option4 = NormalizeImage(question.Option4, out bool isImage4);
			question.Option5 = NormalizeImage(question.Option5, out bool isImage5);

			if (isImage0 || isImage1 || isImage2 || isImage3 || isImage4 || isImage5)
				question.HasImages = true;
		}

		private string NormalizeImage(string textWithImage, out bool isImage)
		{
			string retval;
			isImage = false;

			if (string.IsNullOrEmpty(textWithImage))
				return "";

			HtmlDocument htmlDocument = new HtmlDocument();
			htmlDocument.LoadHtml(textWithImage);

			HtmlNodeCollection imgNodes = htmlDocument.DocumentNode.SelectNodes("//img[@src]");
			if (imgNodes != null)
			{
				foreach(HtmlNode imgNode in imgNodes)
				{
					string imageLink = imgNode.GetAttributeValue("src", "");
					if(!string.IsNullOrEmpty(imageLink))
					{
						string encodedImage = GetBase64EncodedImage($"{_rootUrl}{imageLink}");
						imgNode.SetAttributeValue("src", $"data:image/png;base64,{encodedImage}");
						isImage = true;
					}
				}
			}

			retval = htmlDocument.DocumentNode.OuterHtml;

			return retval;
		}

		private string GetBase64EncodedImage(string imageUrl)
		{
			string retval = "";
			HttpResponseMessage response = _requester.GetRequest(imageUrl).Result;
			if (response.IsSuccessStatusCode)
			{
				byte[] imageBytes = response.Content.ReadAsByteArrayAsync().Result;
				retval = Convert.ToBase64String(imageBytes);
			}
			return retval;
		}

		private static int GetIndexFromAlphabetOption(string alphabetOption)
		{
			switch (alphabetOption.ToLower())
			{
				case "a":
					return 1;
				case "b":
					return 2;
				case "c":
					return 3;
				case "d":
					return 4;
				case "e":
					return 5;
			}
			return 0;
		}

		private void Report(string msg)
		{
			if (_progressOccurred != null)
				_progressOccurred(msg);
		}


	}
}

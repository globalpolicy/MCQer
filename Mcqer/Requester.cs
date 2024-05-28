using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mcqer
{
	internal class Requester
	{
		private static HttpClient _httpClient;
		private static Requester? _requester;
		private Requester(HttpClient httpClient)
		{
			_httpClient = httpClient;
		}

		internal static Requester GetRequester(HttpClient httpClient)
		{
			if (_requester == null)
				_requester = new Requester(httpClient);
			return _requester;
		}

		internal Task<HttpResponseMessage> GetRequest(string url)
		{
			return _httpClient.GetAsync(url);
		}
	}
}

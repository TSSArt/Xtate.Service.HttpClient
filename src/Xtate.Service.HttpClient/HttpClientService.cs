#region Copyright © 2019-2021 Sergii Artemenko

// This file is part of the Xtate project. <https://xtate.net/>
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Xtate.Core;

namespace Xtate.Service
{
	public class HttpClientService : ServiceBase
	{
		private const string MediaTypeApplicationFormUrlEncoded = "application/x-www-form-urlencoded";
		private const string MediaTypeApplicationJson           = "application/json";
		private const string MediaTypeTextHtml                  = "text/html";

		private static readonly FieldInfo DomainTableField = typeof(CookieContainer).GetField(name: "m_domainTable", BindingFlags.Instance | BindingFlags.NonPublic)!;
		private static readonly FieldInfo ListField        = typeof(CookieContainer).Assembly.GetType("System.Net.PathList")!.GetField(name: "m_list", BindingFlags.Instance | BindingFlags.NonPublic)!;

		protected override async ValueTask<DataModelValue> Execute()
		{
			var parameters = Parameters.AsListOrEmpty();

			var method = parameters["method"].AsStringOrDefault() ?? @"get";
			var autoRedirect = parameters["autoRedirect"].AsBooleanOrDefault() ?? true;
			var accept = parameters["accept"].AsStringOrDefault();
			var contentType = parameters["contentType"].AsStringOrDefault();

			var headers = from list in parameters["headers"].AsListOrEmpty()
						  let name = list.AsListOrEmpty()["name"].AsStringOrDefault()
						  let value = list.AsListOrEmpty()["value"].AsStringOrDefault()
						  where !string.IsNullOrEmpty(name) && value is not null
						  select new KeyValuePair<string, string>(name, value);

			var cookies = parameters["cookies"].AsListOrEmpty().Select(CreateCookie);

			var capturesList = parameters["capture"].AsListOrEmpty();
			var captures = from pair in capturesList.KeyValuePairs
						   let capture = pair.Value.AsListOrEmpty()
						   select new Capture
								  {
										  Name = pair.Key,
										  XPaths = GetArray(capture["xpath"]),
										  Attributes = GetArray(capture["attr"]),
										  Regex = capture["regex"].AsStringOrDefault()
								  };

			static string[]? GetArray(DataModelValue val)
			{
				if (val.Type == DataModelValueType.List)
				{
					return val.AsList().Select(p => p.AsString()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
				}

				var str = val.AsStringOrDefault();

				return !string.IsNullOrEmpty(str) ? new[] { str } : null;
			}

			var response = await DoRequest(Source, method, accept, autoRedirect, contentType, headers, cookies, captures.ToArray(), Content, StopToken).ConfigureAwait(false);

			var responseHeaders = new DataModelList();
			foreach (var header in response.Headers)
			{
				responseHeaders.Add(new DataModelList
									{
											{ "name", header.Key },
											{ "value", header.Value }
									});
			}

			var responseCookies = new DataModelList();
			foreach (var cookie in response.Cookies)
			{
				Infrastructure.NotNull(cookie);

				responseCookies.Add(new DataModelList
									{
											{ "name", cookie.Name },
											{ "value", cookie.Value },
											{ "path", cookie.Path },
											{ "domain", cookie.Domain },
											{ "httpOnly", cookie.HttpOnly },
											{ "port", cookie.Port },
											{ "secure", cookie.Secure },
											{ "expires", cookie.Expires != default ? cookie.Expires : default(DataModelValue) }
									});
			}

			return new DataModelList
				   {
						   { "statusCode", response.StatusCode },
						   { "statusDescription", response.StatusDescription },
						   { "webExceptionStatus", response.WebExceptionStatus },
						   { "headers", responseHeaders },
						   { "cookies", responseCookies },
						   { "content", response.Content }
				   };
		}

		private static Cookie CreateCookie(DataModelValue val)
		{
			var cookieList = val.AsListOrEmpty();
			var cookie = new Cookie
						 {
								 Name = cookieList["name"].AsStringOrDefault(),
								 Value = cookieList["value"].AsStringOrDefault(),
								 Path = cookieList["path"].AsStringOrDefault(),
								 Domain = cookieList["domain"].AsStringOrDefault(),
								 Expires = cookieList["expires"].AsDateTimeOrDefault()?.ToDateTime() ?? default,
								 HttpOnly = cookieList["httpOnly"].AsBooleanOrDefault() ?? false,
								 Secure = cookieList["secure"].AsBooleanOrDefault() ?? false
						 };

			var port = cookieList["port"].AsStringOrDefault();

			if (!string.IsNullOrEmpty(port))
			{
				cookie.Port = port;
			}

			return cookie;
		}

		private static HttpContent CreateFormUrlEncodedContent(DataModelValue content)
		{
			var forms = from p in content.AsListOrEmpty()
						let name = p.AsListOrEmpty()["name"].AsStringOrDefault()
						let value = p.AsListOrEmpty()["value"].AsStringOrDefault()
						where !string.IsNullOrEmpty(name) && value is not null
						select new KeyValuePair<string?, string?>(name, value);

			return new FormUrlEncodedContent(forms);
		}

		private static HttpContent CreateJsonContent(DataModelValue content) => new ByteArrayContent(DataModelConverter.ToJsonUtf8Bytes(content));

		private static HttpContent CreateDefaultContent(DataModelValue content) => new StringContent(content.ToObject()?.ToString() ?? string.Empty, Encoding.UTF8);

		private static ValueTask<DataModelValue> FromJsonContent(Stream stream, CancellationToken token) => DataModelConverter.FromJsonAsync(stream, token);

		private static async ValueTask<DataModelValue> FromHtmlContent(Stream stream, string? contentEncoding, Capture[] captures, CancellationToken token)
		{
			var encoding = contentEncoding is not null ? Encoding.GetEncoding(contentEncoding) : Encoding.UTF8;

			var htmlDocument = new HtmlDocument();

			string html;
			using (var streamReader = new StreamReader(stream.InjectCancellationToken(token), encoding))
			{
				html = await streamReader.ReadToEndAsync().ConfigureAwait(false);
			}

			htmlDocument.LoadHtml(html);

			return CaptureData(htmlDocument, captures);
		}

		private static async ValueTask<Response> DoRequest(Uri? requestUri, string method, string? accept, bool autoRedirect, string? contentType,
														   IEnumerable<KeyValuePair<string, string>> headers, IEnumerable<Cookie> cookies,
														   Capture[] captures, DataModelValue content, CancellationToken token)
		{
			Infrastructure.NotNull(requestUri);

			var request = WebRequest.CreateHttp(requestUri);

			request.Method = method;
			request.AllowAutoRedirect = autoRedirect;

			if (accept is not null)
			{
				request.Accept = accept;
			}

			foreach (var header in headers)
			{
				request.Headers.Add(header.Key, header.Value);
			}

			var cookieContainer = new CookieContainer();

			foreach (var cookie in cookies)
			{
				cookieContainer.Add(cookie);
			}

			request.CookieContainer = cookieContainer;

			await WriteContent(request, contentType, content, token).ConfigureAwait(false);

			var result = new Response();

			HttpWebResponse response;
			try
			{
				response = await GetResponse(request, token).ConfigureAwait(false);
			}
			catch (WebException ex)
			{
				if (ex.Response is null)
				{
					return new Response();
				}

				response = (HttpWebResponse) ex.Response;

				result.WebExceptionStatus = ex.Status.ToString();
			}

			result.StatusCode = (int) response.StatusCode;
			result.StatusDescription = response.StatusDescription;

			var stream = response.GetResponseStream();
			Infrastructure.NotNull(stream);
			await using (stream.ConfigureAwait(false))
			{
				var responseContentType = new ContentType(response.ContentType);

				if (responseContentType.MediaType == MediaTypeApplicationJson)
				{
					result.Content = await FromJsonContent(stream, token).ConfigureAwait(false);
				}
				else if (responseContentType.MediaType == MediaTypeTextHtml)
				{
					result.Content = await FromHtmlContent(stream, response.ContentEncoding, captures, token).ConfigureAwait(false);
				}
			}

			result.Headers = from key in response.Headers.AllKeys select new KeyValuePair<string, string?>(key, response.Headers[key]);

			AppendCookies(requestUri, cookieContainer, response);

			result.Cookies = from object pathList in ((Hashtable) DomainTableField.GetValue(cookieContainer)!).Values
							 from IEnumerable cookieList in ((SortedList) ListField.GetValue(pathList)!).Values
							 from Cookie cookie in cookieList
							 select cookie;

			return result;
		}

		private static async Task<HttpWebResponse> GetResponse(HttpWebRequest request, CancellationToken token)
		{
			var registration = token.Register(request.Abort, useSynchronizationContext: false);
			await using (registration.ConfigureAwait(false))
			{
				try
				{
					return (HttpWebResponse) await request.GetResponseAsync().ConfigureAwait(false);
				}
				catch (WebException ex)
				{
					if (token.IsCancellationRequested)
					{
						throw new OperationCanceledException(ex.Message, ex, token);
					}

					throw;
				}
			}
		}

		private static void AppendCookies(Uri? uri, CookieContainer cookieContainer, HttpWebResponse response)
		{
			var list = response.Headers.GetValues("Set-Cookie");

			if (list is null)
			{
				return;
			}

			Infrastructure.NotNull(uri);
			var uriBuilder = new UriBuilder(uri);

			foreach (var header in list)
			{
				foreach (var path in GetPaths(header))
				{
					uriBuilder.Path = path;

					cookieContainer.SetCookies(uriBuilder.Uri, header);
				}
			}

			static IEnumerable<string> GetPaths(string header)
			{
				const string pathPrefix = "path=";

				var p1 = 0;
				while ((p1 = header.IndexOf(pathPrefix, p1, StringComparison.Ordinal)) >= 0)
				{
					p1 += pathPrefix.Length;

					var p2 = header.IndexOf(value: ';', p1);

					var path = p2 < 0 ? header[p1..] : header[p1..p2];

					yield return path.Trim().Trim('\"');
				}
			}
		}

		private static async ValueTask WriteContent(WebRequest request, string? contentType, DataModelValue content, CancellationToken token)
		{
			if (contentType is null)
			{
				return;
			}

			request.ContentType = contentType;

			var stream = await request.GetRequestStreamAsync().ConfigureAwait(false);
			await using (stream.ConfigureAwait(false))
			{
				switch (contentType)
				{
					case MediaTypeApplicationFormUrlEncoded:
					{
						using var httpContent = CreateFormUrlEncodedContent(content);
						await CopyContentToStreamAsync(httpContent, stream, token).ConfigureAwait(false);
						break;
					}
					case MediaTypeApplicationJson:
					{
						using var httpContent = CreateJsonContent(content);
						await CopyContentToStreamAsync(httpContent, stream, token).ConfigureAwait(false);
						break;
					}
					default:
					{
						using var httpContent = CreateDefaultContent(content);
						await CopyContentToStreamAsync(httpContent, stream, token).ConfigureAwait(false);
						break;
					}
				}
			}
		}

#if NET461 || NETSTANDARD2_0
		private static Task CopyContentToStreamAsync(HttpContent httpContent, Stream stream, CancellationToken token) => httpContent.CopyToAsync(stream.InjectCancellationToken(token));
#else
		private static Task CopyContentToStreamAsync(HttpContent httpContent, Stream stream, CancellationToken token) => httpContent.CopyToAsync(stream, token);
#endif

		private static DataModelValue CaptureData(HtmlDocument htmlDocument, Capture[] captures)
		{
			var list = new DataModelList();

			foreach (var capture in captures)
			{
				var result = CaptureEntry(htmlDocument, capture.XPaths, capture.Attributes, capture.Regex);

				if (!result.IsUndefined())
				{
					list.Add(capture.Name, result);
				}
			}

			return list;
		}

		private static DataModelValue CaptureEntry(HtmlDocument htmlDocument, string[]? xpaths, string[]? attrs, string? pattern)
		{
			if (xpaths is null)
			{
				return CaptureInNode(htmlDocument.DocumentNode, attrs, pattern);
			}

			var array = new DataModelList();

			foreach (var xpath in xpaths)
			{
				var nodes = htmlDocument.DocumentNode.SelectNodes(xpath);

				if (nodes is null)
				{
					continue;
				}

				foreach (var node in nodes)
				{
					var result = CaptureInNode(node, attrs, pattern);

					if (!result.IsUndefined())
					{
						array.Add(result);
					}
				}
			}

			return array;
		}

		private static DataModelValue CaptureInNode(HtmlNode node, string[]? attrs, string? pattern)
		{
			if (attrs is null)
			{
				return CaptureInText(node.InnerHtml, pattern);
			}

			var list = new DataModelList();

			foreach (var attr in attrs)
			{
				var value = attr.StartsWith(value: @"::", StringComparison.Ordinal) ? GetSpecialAttributeValue(node, attr) : node.GetAttributeValue(attr, def: null);

				if (value is null)
				{
					return default;
				}

				list.Add(attr, CaptureInText(value, pattern));
			}

			return list;
		}

		private static string? GetSpecialAttributeValue(HtmlNode node, string attr) =>
				attr switch
				{
						"::value" => GetHtmlValue(node),
						_ => null
				};

		private static string? GetHtmlValue(HtmlNode node) =>
				node.Name switch
				{
						"input" => GetInputValue(node),
						"textarea" => GetInputValue(node),
						"select" => GetSelectValue(node),
						_ => null
				};

		private static string? GetSelectValue(HtmlNode node)
		{
			var selected = node.ChildNodes.FirstOrDefault(n => n.Name == @"option" && n.Attributes.Contains(@"selected"))
						   ?? node.ChildNodes.FirstOrDefault(n => n.Name == @"option");

			return selected is not null ? GetValue(selected, check: false) : null;
		}

		private static string? GetInputValue(HtmlNode node) =>
				node.GetAttributeValue(name: @"type", def: null) switch
				{
						"radio" => GetValue(node, check: true),
						"checkbox" => GetValue(node, check: true),
						_ => GetValue(node, check: false)
				};

		private static string? GetValue(HtmlNode node, bool check)
		{
			if (check && !node.Attributes.Contains(@"checked"))
			{
				return null;
			}

			return node.GetAttributeValue(name: @"value", def: null) ?? node.InnerText;
		}

		private static DataModelValue CaptureInText(string text, string? pattern)
		{
			if (pattern is null)
			{
				return text;
			}

			var regex = new Regex(pattern);
			var match = regex.Match(text);

			if (!match.Success)
			{
				return default;
			}

			if (match.Groups.Count == 1)
			{
				return match.Groups[0].Value;
			}

			var groupNames = regex.GetGroupNames();

			var list = new DataModelList();
			foreach (var name in groupNames)
			{
				list.Add(name, match.Groups[name].Value);
			}

			return list;
		}

		private struct Response
		{
			public int                                        StatusCode         { get; set; }
			public string                                     StatusDescription  { get; set; }
			public string                                     WebExceptionStatus { get; set; }
			public DataModelValue                             Content            { get; set; }
			public IEnumerable<KeyValuePair<string, string?>> Headers            { get; set; }
			public IEnumerable<Cookie?>                       Cookies            { get; set; }
		}

		private struct Capture
		{
			public string    Name       { get; set; }
			public string[]? XPaths     { get; set; }
			public string[]? Attributes { get; set; }
			public string?   Regex      { get; set; }
		}
	}
}
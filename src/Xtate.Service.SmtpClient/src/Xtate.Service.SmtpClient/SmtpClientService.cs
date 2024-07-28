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

using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace Xtate.Service
{
	public class SmtpClientService : ServiceBase
	{
		protected override async ValueTask<DataModelValue> Execute()
		{
			var parameters = Parameters.AsListOrEmpty();
			var host = parameters["server"].AsString();
			var port = parameters["port"].AsNumberOrDefault();
			using var client = new SmtpClient(host, port is { } p ? (int) p : 25);

			var fromEmail = parameters["from"].AsString();
			var fromName = parameters["fromName"].AsStringOrDefault();
			var from = fromName is not null ? new MailAddress(fromEmail, fromName, Encoding.UTF8) : new MailAddress(fromEmail);

			var toEmail = parameters["to"].AsString();
			var toName = parameters["toName"].AsStringOrDefault();
			var to = toName is not null ? new MailAddress(toEmail, toName, Encoding.UTF8) : new MailAddress(toEmail);

			var textBody = parameters["body"].AsStringOrDefault();
			var htmlBody = parameters["htmlBody"].AsStringOrDefault();

			using var message = new MailMessage(from, to)
								{
										Body = htmlBody ?? textBody,
										IsBodyHtml = htmlBody is not null,
										BodyEncoding = Encoding.UTF8,
										Subject = parameters["subject"].AsStringOrDefault(),
										SubjectEncoding = Encoding.UTF8
								};

			await client.SendMailAsync(message).ConfigureAwait(false);

			return default;
		}
	}
}
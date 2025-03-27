using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace WhatsAppDDAuth.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WhatsappController : ControllerBase
    {
        private readonly string _twilioNumber;
        private readonly string _twilioSid;
        private readonly string _twilioToken;
        private readonly ILogger<WhatsappController> _logger;

        private static readonly Dictionary<string, string> Responses = new()
        {
            { "1", "🌱 Agriculture Loan: Quick approval, minimal paperwork. Would you like to proceed?" },
            { "2", "🤝 Support & Help: How can we assist you today?" }
        };

        private static readonly string WelcomeMessage = """
        🌾 Welcome to Sugee.io! 🌾

        We offer:
        1. Apply for an Agriculture Loan
        2. Customer Support

        Reply with 1 or 2 to proceed.
        """;

        public WhatsappController(ILogger<WhatsappController> logger)
        {
            _logger = logger;

            // Load Twilio credentials from environment variables
            _twilioNumber = Environment.GetEnvironmentVariable("TWILIO_NUMBER");
            _twilioSid = Environment.GetEnvironmentVariable("TWILIO_SID");
            _twilioToken = Environment.GetEnvironmentVariable("TWILIO_TOKEN");

            // Validate Twilio credentials
            if (string.IsNullOrEmpty(_twilioSid) || string.IsNullOrEmpty(_twilioToken) || string.IsNullOrEmpty(_twilioNumber))
            {
                _logger.LogError("❌ Twilio credentials are missing.");
                throw new ArgumentException("Twilio credentials are missing.");
            }

            // Initialize Twilio client
            TwilioClient.Init(_twilioSid, _twilioToken);
        }

        private async Task SendMessageAsync(string to, string body)
        {
            try
            {
                await MessageResource.CreateAsync(
                    from: new PhoneNumber($"whatsapp:{_twilioNumber}"),
                    to: new PhoneNumber($"whatsapp:{to}"),
                    body: body
                );

                _logger.LogInformation($"✅ Message sent to {to}: {body}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Failed to send message to {to}: {ex.Message}");
            }
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> ReceiveMessage()
        {
            try
            {
                var form = await Request.ReadFormAsync();
                string incomingMsg = form["Body"].ToString().Trim().ToLower();
                string sender = form["From"].ToString().Replace("whatsapp:", "");

                _logger.LogInformation($"📩 Received '{incomingMsg}' from {sender}");

                string reply = incomingMsg switch
                {
                    var msg when msg == "hi" || msg == "hello" || msg.Contains("hi") => WelcomeMessage,
                    var msg when Responses.ContainsKey(msg) => Responses[msg],
                    _ => "❌ Invalid option. Reply with 1 or 2."
                };

                await SendMessageAsync(sender, reply);
                _logger.LogInformation($"🔔 Reply sent to {sender}: {reply}");

                return Content("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Response></Response>", "text/xml");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❗ Error processing request: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal Server Error");
            }
        }
    }
}

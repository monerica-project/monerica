using DirectoryManager.Services.Interfaces;
using DirectoryManager.Services.Models;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace DirectoryManager.Services.Exceptions
{

    public class SendGridDeliveryException : Exception
    {
        public int StatusCode { get; }
        public string ResponseBody { get; }
        public bool IsTransient => this.StatusCode == 429 || this.StatusCode >= 500;

        public SendGridDeliveryException(int statusCode, string responseBody)
            : base($"SendGrid send failed. Status: {statusCode}. Body: {responseBody}")
        {
            this.StatusCode = statusCode;
            this.ResponseBody = responseBody;
        }
    }
}
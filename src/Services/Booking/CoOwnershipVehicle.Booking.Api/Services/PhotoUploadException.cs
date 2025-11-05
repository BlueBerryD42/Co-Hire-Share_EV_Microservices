using Microsoft.AspNetCore.Http;

namespace CoOwnershipVehicle.Booking.Api.Services;

public class PhotoUploadException : Exception
{
    public int StatusCode { get; }

    public PhotoUploadException(string message, int statusCode = StatusCodes.Status400BadRequest)
        : base(message)
    {
        StatusCode = statusCode;
    }
}

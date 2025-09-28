using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Net;
using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Payment.Api.Services;

public interface IVnPayService
{
    string CreatePaymentUrl(VnPayPaymentRequest request);
    VnPayPaymentResponse ProcessPaymentCallback(IQueryCollection queryParams);
    bool ValidateSignature(IQueryCollection queryParams, string secretKey);
}

public class VnPayService : IVnPayService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<VnPayService> _logger;

    public VnPayService(IConfiguration configuration, ILogger<VnPayService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string CreatePaymentUrl(VnPayPaymentRequest request)
    {
        var baseUrl = Environment.GetEnvironmentVariable("VNPAY_PAYMENT_URL");
        var tmnCode = Environment.GetEnvironmentVariable("VNPAY_TMN_CODE");
        var hashSecret = Environment.GetEnvironmentVariable("VNPAY_HASH_SECRET");
        var returnUrl = Environment.GetEnvironmentVariable("VNPAY_RETURN_URL");

        var vnpay = new VnPayLibrary();
        
        // Set VNPay parameters
        vnpay.AddRequestData("vnp_Version", "2.1.0");
        vnpay.AddRequestData("vnp_Command", "pay");
        vnpay.AddRequestData("vnp_TmnCode", tmnCode);
        vnpay.AddRequestData("vnp_Amount", (request.Amount * 100).ToString()); // VNPay requires amount in VND cents
        vnpay.AddRequestData("vnp_CurrCode", "VND");
        vnpay.AddRequestData("vnp_TxnRef", request.OrderId);
        vnpay.AddRequestData("vnp_OrderInfo", request.OrderInfo);
        vnpay.AddRequestData("vnp_OrderType", request.OrderType);
        vnpay.AddRequestData("vnp_Locale", request.Locale ?? "vn");
        vnpay.AddRequestData("vnp_ReturnUrl", returnUrl);
        vnpay.AddRequestData("vnp_IpAddr", request.IpAddress);
        vnpay.AddRequestData("vnp_CreateDate", request.CreatedDate.ToString("yyyyMMddHHmmss"));

        // Bank code (optional)
        if (!string.IsNullOrEmpty(request.BankCode))
        {
            vnpay.AddRequestData("vnp_BankCode", request.BankCode);
        }

        var paymentUrl = vnpay.CreateRequestUrl(baseUrl!, hashSecret!);
        
        _logger.LogInformation("VNPay payment URL created for order {OrderId}, amount {Amount} VND", 
            request.OrderId, request.Amount);

        return paymentUrl;
    }

    public VnPayPaymentResponse ProcessPaymentCallback(IQueryCollection queryParams)
    {
        var vnPayConfig = _configuration.GetSection("VnPay");
        var hashSecret = vnPayConfig["HashSecret"];

        var vnpay = new VnPayLibrary();
        
        // Add all parameters except signature
        foreach (var param in queryParams)
        {
            if (param.Key != "vnp_SecureHash" && param.Key != "vnp_SecureHashType")
            {
                vnpay.AddResponseData(param.Key, param.Value!);
            }
        }

        var orderId = vnpay.GetResponseData("vnp_TxnRef");
        var vnpayTranId = vnpay.GetResponseData("vnp_TransactionNo");
        var responseCode = vnpay.GetResponseData("vnp_ResponseCode");
        var secureHash = queryParams["vnp_SecureHash"];

        // Validate signature
        var isValidSignature = vnpay.ValidateSignature(secureHash!, hashSecret!);

        if (!isValidSignature)
        {
            _logger.LogWarning("Invalid VNPay signature for order {OrderId}", orderId);
            return new VnPayPaymentResponse
            {
                Success = false,
                Message = "Invalid signature",
                OrderId = orderId,
                TransactionId = vnpayTranId,
                ResponseCode = responseCode
            };
        }

        // Check response code
        var success = responseCode == "00"; // 00 = Success

        var response = new VnPayPaymentResponse
        {
            Success = success,
            Message = GetResponseMessage(responseCode),
            OrderId = orderId,
            TransactionId = vnpayTranId,
            ResponseCode = responseCode,
            Amount = decimal.Parse(vnpay.GetResponseData("vnp_Amount")) / 100, // Convert from cents
            BankCode = vnpay.GetResponseData("vnp_BankCode"),
            PayDate = DateTime.ParseExact(vnpay.GetResponseData("vnp_PayDate"), "yyyyMMddHHmmss", null)
        };

        _logger.LogInformation("VNPay payment callback processed for order {OrderId}, success: {Success}", 
            orderId, success);

        return response;
    }

    public bool ValidateSignature(IQueryCollection queryParams, string secretKey)
    {
        var vnpay = new VnPayLibrary();
        
        foreach (var param in queryParams)
        {
            if (param.Key != "vnp_SecureHash" && param.Key != "vnp_SecureHashType")
            {
                vnpay.AddResponseData(param.Key, param.Value!);
            }
        }

        var secureHash = queryParams["vnp_SecureHash"];
        return vnpay.ValidateSignature(secureHash!, secretKey);
    }

    private string GetResponseMessage(string responseCode)
    {
        return responseCode switch
        {
            "00" => "Giao dịch thành công",
            "07" => "Trừ tiền thành công. Giao dịch bềEnghi ngềE(liên quan tới lừa đảo, giao dịch bất thường).",
            "09" => "Giao dịch không thành công do: Thẻ/Tài khoản của khách hàng chưa đăng ký dịch vụ InternetBanking tại ngân hàng.",
            "10" => "Giao dịch không thành công do: Khách hàng xác thực thông tin thẻ/tài khoản không đúng quá 3 lần",
            "11" => "Giao dịch không thành công do: Đã hết hạn chềEthanh toán. Xin quý khách vui lòng thực hiện lại giao dịch.",
            "12" => "Giao dịch không thành công do: Thẻ/Tài khoản của khách hàng bềEkhóa.",
            "13" => "Giao dịch không thành công do Quý khách nhập sai mật khẩu xác thực giao dịch (OTP). Xin quý khách vui lòng thực hiện lại giao dịch.",
            "24" => "Giao dịch không thành công do: Khách hàng hủy giao dịch",
            "51" => "Giao dịch không thành công do: Tài khoản của quý khách không đủ sềEdư đềEthực hiện giao dịch.",
            "65" => "Giao dịch không thành công do: Tài khoản của Quý khách đã vượt quá hạn mức giao dịch trong ngày.",
            "75" => "Ngân hàng thanh toán đang bảo trì.",
            "79" => "Giao dịch không thành công do: KH nhập sai mật khẩu thanh toán quá sềElần quy định. Xin quý khách vui lòng thực hiện lại giao dịch",
            "99" => "Các lỗi khác (lỗi còn lại, không có trong danh sách mã lỗi đã liệt kê)",
            _ => "Lỗi không xác định"
        };
    }
}

// VNPay Library Helper Class
public class VnPayLibrary
{
    private readonly SortedDictionary<string, string> _requestData = new();
    private readonly SortedDictionary<string, string> _responseData = new();

    public void AddRequestData(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _requestData[key] = value;
        }
    }

    public void AddResponseData(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _responseData[key] = value;
        }
    }

    public string GetResponseData(string key)
    {
        return _responseData.TryGetValue(key, out var value) ? value : string.Empty;
    }

    public string CreateRequestUrl(string baseUrl, string vnpHashSecret)
    {
        var data = new StringBuilder();
        
        foreach (var kv in _requestData.Where(kv => !string.IsNullOrEmpty(kv.Value)))
        {
            data.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
        }

        var queryString = data.ToString();
        if (queryString.Length > 0)
        {
            queryString = queryString.Remove(queryString.Length - 1, 1);
        }

        var signData = queryString;
        var vnpSecureHash = Utils.HmacSHA512(vnpHashSecret, signData);
        
        return $"{baseUrl}?{queryString}&vnp_SecureHash={vnpSecureHash}";
    }

    public bool ValidateSignature(string inputHash, string secretKey)
    {
        var data = new StringBuilder();
        
        foreach (var kv in _responseData.Where(kv => !string.IsNullOrEmpty(kv.Value)))
        {
            data.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
        }

        var queryString = data.ToString();
        if (queryString.Length > 0)
        {
            queryString = queryString.Remove(queryString.Length - 1, 1);
        }

        var signData = queryString;
        var vnpSecureHash = Utils.HmacSHA512(secretKey, signData);

        return vnpSecureHash.Equals(inputHash, StringComparison.InvariantCultureIgnoreCase);
    }
}

// Utility class for VNPay
public static class Utils
{
    public static string HmacSHA512(string key, string inputData)
    {
        var hash = new StringBuilder();
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var inputBytes = Encoding.UTF8.GetBytes(inputData);
        
        using (var hmac = new HMACSHA512(keyBytes))
        {
            var hashValue = hmac.ComputeHash(inputBytes);
            foreach (var theByte in hashValue)
            {
                hash.Append(theByte.ToString("x2"));
            }
        }

        return hash.ToString();
    }
}

// VNPay DTOs
public class VnPayPaymentRequest
{
    public string OrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string OrderInfo { get; set; } = string.Empty;
    public string OrderType { get; set; } = "other";
    public string? Locale { get; set; } = "vn";
    public string IpAddress { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public string? BankCode { get; set; }
}

public class VnPayPaymentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public string ResponseCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? BankCode { get; set; }
    public DateTime PayDate { get; set; }
}

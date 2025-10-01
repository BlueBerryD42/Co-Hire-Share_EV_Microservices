using CoOwnershipVehicle.WCF.Contracts;
using CoOwnershipVehicle.WCF.Service;
using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

var builder = WebApplication.CreateBuilder(args);

// Add WCF services
builder.Services
    .AddServiceModelServices()
    .AddServiceModelMetadata()
    .AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();

// Configure Kestrel for HTTP only
builder.WebHost.UseKestrel(options =>
{
    options.ListenAnyIP(5020); // HTTP
    // UseNetTcp handles it
});

builder.WebHost.UseNetTcp(5021);

var app = builder.Build();

// Configure WCF endpoints (Address)
app.UseServiceModel(builder =>
{
    builder.AddService<VehicleManagementService>();
    
    // HTTP endpoint
    builder.AddServiceEndpoint<VehicleManagementService, IVehicleManagementService>(
        new CoreWCF.BasicHttpBinding(),
        "/VehicleService/BasicHttp"
    );

// TCP endpoint with security disabled for demo
//Transport: encrypts at the TCP layer, requires Windows/domain trust.
//
//Message: encrypts at the SOAP message level, requires certificates.
//
//TransportWithMessageCredential: mix of both, used in enterprise scenarios.

    var netTcpBinding = new CoreWCF.NetTcpBinding();
    //netTcpBinding.Security.Mode = CoreWCF.SecurityMode.None;
    //builder.AddServiceEndpoint<VehicleManagementService, IVehicleManagementService>(
    //    netTcpBinding,
    //    "/VehicleService/NetTcp"
    //);

    builder.AddServiceEndpoint<VehicleManagementService, IVehicleManagementService>(
    new WSHttpBinding(SecurityMode.Transport), // HTTPS only
    "https://localhost:5022/VehicleService/Secure");


    //var secureBinding = new WSHttpBinding(SecurityMode.=)
    //{
    //    Security = new WSHttpSecurity
    //    {
    //        Message = new NonDualMessageSecurityOverHttp
    //        {
    //            ClientCredentialType = MessageCredentialType.UserName
    //        }
    //    }
    //};

    //serviceBuilder.AddServiceEndpoint<VehicleManagementService, IVehicleManagementService>(
    //    secureBinding,
    //    "http://localhost:5023/VehicleService/MessageSecurity");

    // Enable metadata exchange
    var serviceMetadataBehavior = app.Services.GetRequiredService<ServiceMetadataBehavior>();
    serviceMetadataBehavior.HttpGetEnabled = true;
});

Console.WriteLine("=== Co-Ownership Vehicle WCF Service Host ===");
Console.WriteLine("WCF Service is running on CoreWCF (.NET 8)");
Console.WriteLine("");
Console.WriteLine("Available endpoints:");
Console.WriteLine("  HTTP (BasicHttpBinding): http://localhost:5020/VehicleService/BasicHttp");
Console.WriteLine("  TCP (NetTcpBinding):     net.tcp://localhost:5021/VehicleService/NetTcp");
Console.WriteLine("  WSDL:                    http://localhost:5020/?wsdl");
Console.WriteLine("");
Console.WriteLine("Press Ctrl+C to stop...");
Console.WriteLine("");

app.Run();

//Invoke-RestMethod -Uri "http://localhost:5020/VehicleService/BasicHttp?wsdl" -ContentType "text/xml"
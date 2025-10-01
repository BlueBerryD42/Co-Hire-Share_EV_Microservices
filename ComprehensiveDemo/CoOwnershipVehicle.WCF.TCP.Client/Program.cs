using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using System.Diagnostics;
using CoOwnershipVehicle.WCF.Contracts; // Use the shared contracts

namespace CoOwnershipVehicle.WCF.TCP.Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Test endpoints (matching the server configuration)
            string httpEndpoint = "http://localhost:5020/VehicleService/BasicHttp";
            string tcpEndpoint = "net.tcp://localhost:5021/VehicleService/NetTcp";

            Console.WriteLine("=== WCF Service Test Client ===");
            Console.WriteLine("Running WCF tests...\n");

            // Test HTTP
            await TestEndpoint(httpEndpoint, new BasicHttpBinding());

            Console.WriteLine("\n" + new string('-', 50) + "\n");

            // Test TCP
            await TestEndpoint(tcpEndpoint, new NetTcpBinding(SecurityMode.None));

            Console.WriteLine("\n" + new string('=', 60) + "\n");

            // Performance Comparison
            await PerformanceComparison(httpEndpoint, tcpEndpoint);

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        static async Task TestEndpoint(string endpointAddress, Binding binding)
        {
            Console.WriteLine($"Testing {binding.Scheme.ToUpper()} endpoint:");
            Console.WriteLine($"  Address: {endpointAddress}");

            ChannelFactory<IVehicleManagementService>? channelFactory = null;
            IVehicleManagementService? client = null;

            try
            {
                // Create channel factory
                channelFactory = new ChannelFactory<IVehicleManagementService>(
                    binding,
                    new EndpointAddress(endpointAddress)
                );

                // Create client
                client = channelFactory.CreateChannel();

                // Open the channel
                ((IClientChannel)client).Open();
                Console.WriteLine("  Connection established");

                // Test GetServiceInfo
                Console.WriteLine("\n  Calling GetServiceInfoAsync()...");
                var info = await client.GetServiceInfoAsync();
                Console.WriteLine($"  Service info: {info}");

                // Test GetAvailableVehicles
                Console.WriteLine("\n  Calling GetAvailableVehiclesAsync()...");
                var vehicles = await client.GetAvailableVehiclesAsync();
                Console.WriteLine($"  Available vehicles: {vehicles.Count}");

                if (vehicles.Count > 0)
                {
                    Console.WriteLine("\n  Sample vehicles:");
                    for (int i = 0; i < Math.Min(3, vehicles.Count); i++)
                    {
                        var v = vehicles[i];
                        Console.WriteLine($"     {v.Brand} {v.Model} ({v.Year}) - {v.LicensePlate}");
                        Console.WriteLine($"      Price: ${v.PricePerDay}/day, Status: {v.Status}");
                    }
                }

                // Close properly
                ((IClientChannel)client).Close();
                channelFactory.Close();

                Console.WriteLine("\n  Test completed successfully");
            }
            catch (EndpointNotFoundException ex)
            {
                Console.WriteLine($"  Endpoint not found: {ex.Message}");
                Console.WriteLine("  Make sure the WCF service is running");
            }
            catch (CommunicationException ex)
            {
                Console.WriteLine($"  Communication error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error: {ex.Message}");
                Console.WriteLine($"  Type: {ex.GetType().Name}");
            }
            finally
            {
                // Cleanup
                try
                {
                    if (client != null && ((IClientChannel)client).State != CommunicationState.Closed)
                        ((IClientChannel)client).Abort();
                    if (channelFactory != null && channelFactory.State != CommunicationState.Closed)
                        channelFactory.Abort();
                }
                catch { }
            }
        }

        static async Task PerformanceComparison(string httpEndpoint, string tcpEndpoint)
        {
            Console.WriteLine("PERFORMANCE COMPARISON: HTTP vs TCP");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine("NOTE: Timing starts AFTER connection is established");
            Console.WriteLine("      to measure pure message throughput.\n");
            
            const int iterations = 500;
            Console.WriteLine($"Running {iterations} calls to each endpoint...\n");

            // Test HTTP Performance
            Console.WriteLine("Testing HTTP (BasicHttpBinding) Performance...");
            var httpTime = await MeasurePerformance(httpEndpoint, new BasicHttpBinding(), iterations);

            Console.WriteLine("\nTesting TCP (NetTcpBinding) Performance...");
            var tcpTime = await MeasurePerformance(tcpEndpoint, new NetTcpBinding(SecurityMode.None), iterations);

            // Display Results
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("RESULTS:");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"HTTP Total Time:     {httpTime.TotalMilliseconds:N0} ms");
            Console.WriteLine($"HTTP Avg Per Call:   {httpTime.TotalMilliseconds / iterations:N2} ms");
            Console.WriteLine();
            Console.WriteLine($"TCP Total Time:      {tcpTime.TotalMilliseconds:N0} ms");
            Console.WriteLine($"TCP Avg Per Call:    {tcpTime.TotalMilliseconds / iterations:N2} ms");
            Console.WriteLine();
            
            var speedup = httpTime.TotalMilliseconds / tcpTime.TotalMilliseconds;
            var improvement = ((httpTime.TotalMilliseconds - tcpTime.TotalMilliseconds) / httpTime.TotalMilliseconds) * 100;
            
            if (tcpTime.TotalMilliseconds < httpTime.TotalMilliseconds)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ TCP is {speedup:N2}x FASTER than HTTP");
                Console.WriteLine($"✓ Performance Improvement: {improvement:N1}%");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"! TCP is {(1/speedup):N2}x SLOWER than HTTP");
                Console.WriteLine($"! Performance Difference: {-improvement:N1}% slower");
                Console.WriteLine("  (This can happen on localhost with small payloads)");
                Console.ResetColor();
            }
            Console.WriteLine();
            
            // Visual comparison
            DrawPerformanceChart(httpTime.TotalMilliseconds, tcpTime.TotalMilliseconds);
        }

        static async Task<TimeSpan> MeasurePerformance(string endpointAddress, Binding binding, int iterations)
        {
            int successCount = 0;
            ChannelFactory<IVehicleManagementService>? channelFactory = null;
            IVehicleManagementService? client = null;

            try
            {
                // Create connection ONCE and reuse it
                channelFactory = new ChannelFactory<IVehicleManagementService>(
                    binding,
                    new EndpointAddress(endpointAddress)
                );

                client = channelFactory.CreateChannel();
                ((IClientChannel)client).Open();

                // Warm-up call (not counted)
                await client.GetServiceInfoAsync();

                // Start timing AFTER connection is established
                var stopwatch = Stopwatch.StartNew();

                for (int i = 0; i < iterations; i++)
                {
                    try
                    {
                        await client.GetServiceInfoAsync();
                        successCount++;
                        
                        // Progress indicator
                        if ((i + 1) % 10 == 0)
                        {
                            Console.Write($"  Progress: {i + 1}/{iterations} calls\r");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\n  Error on call {i + 1}: {ex.Message}");
                    }
                }

                stopwatch.Stop();
                Console.WriteLine($"  Completed: {successCount}/{iterations} calls      ");

                ((IClientChannel)client).Close();
                channelFactory.Close();

                return stopwatch.Elapsed;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n  Failed to connect: {ex.Message}");
                return TimeSpan.Zero;
            }
            finally
            {
                try
                {
                    if (client != null && ((IClientChannel)client).State != CommunicationState.Closed)
                        ((IClientChannel)client).Abort();
                    if (channelFactory != null && channelFactory.State != CommunicationState.Closed)
                        channelFactory.Abort();
                }
                catch { }
            }
        }

        static void DrawPerformanceChart(double httpMs, double tcpMs)
        {
            Console.WriteLine("Visual Performance Comparison:");
            Console.WriteLine(new string('-', 60));
            
            var maxBarLength = 50;
            var maxTime = Math.Max(httpMs, tcpMs);
            
            var httpBarLength = (int)((httpMs / maxTime) * maxBarLength);
            var tcpBarLength = (int)((tcpMs / maxTime) * maxBarLength);
            
            Console.Write("HTTP: ");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write(new string('█', httpBarLength));
            Console.ResetColor();
            Console.WriteLine($" {httpMs:N0} ms");
            
            Console.Write("TCP:  ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(new string('█', tcpBarLength));
            Console.ResetColor();
            Console.WriteLine($" {tcpMs:N0} ms");
            
            Console.WriteLine(new string('-', 60));
        }
    }
}
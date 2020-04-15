using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Azure.Management.Media;
using Microsoft.Azure.Management.Media.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;
using Microsoft.Rest.Azure.Authentication;

namespace AzureMediaServices_StreamingEndpoint_AutoScale
{
    class Program
    {

        static string _defaultStreamingEndpointName = "default";
        static int _defaultChangeValue = 1;

        static async Task Main(string[] args)
        {

            Console.WriteLine("Start Job");

            AzureMediaServicesConfig config = new AzureMediaServicesConfig(
                new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build());

            try
            {
                Console.WriteLine("1. Login Azure Media Services");
                IAzureMediaServicesClient client = await CreateMediaServicesClientAsync(config);
                Console.WriteLine("connected");

                Console.WriteLine("2. Get StreamingEndpoint State");
                var streamingEndpoint = client.StreamingEndpoints.Get(config.ResourceGroup,
                    config.AccountName,
                    _defaultStreamingEndpointName);

                if (streamingEndpoint.ResourceState == StreamingEndpointResourceState.Stopped
                    || streamingEndpoint.ResourceState == StreamingEndpointResourceState.Running)
                {
                    Console.WriteLine("3. Update StreamingEndpoint ScaleUnit");
                    Console.WriteLine($"  StreamingEndpoint-Before #{streamingEndpoint.ScaleUnits}");
                    var NewScaleUnitNumber = streamingEndpoint.ScaleUnits + _defaultChangeValue;
                    await client.StreamingEndpoints.BeginScaleAsync(
                        config.ResourceGroup,
                        config.AccountName,
                        _defaultStreamingEndpointName,
                        NewScaleUnitNumber);
                    Console.WriteLine($"  StreamingEndpoint-After  #{NewScaleUnitNumber}");

                }else
                {
                    Console.WriteLine("3. Skip update ScaleUnit because other operation process is working. Please retry this later.");
                }

                client.Dispose();
                Console.WriteLine("Completed!");

            }
            catch (Exception exception)
            {
                if (exception.Source.Contains("ActiveDirectory"))
                {
                    Console.Error.WriteLine("Tip: Make sure that you have filled out the appsettings.json file before running this sample.");
                }

                Console.Error.WriteLine($"{exception.Message}");

                ApiErrorException apiException = exception.GetBaseException() as ApiErrorException;
                if (apiException != null)
                {
                    Console.Error.WriteLine(
                        $"ERROR: API call failed with error code '{apiException.Body.Error.Code}' and message '{apiException.Body.Error.Message}'.");
                }
            }
        }

        private static async Task<ServiceClientCredentials> GetCredentialsAsync(AzureMediaServicesConfig config)
        {
            // Use ApplicationTokenProvider.LoginSilentWithCertificateAsync or UserTokenProvider.LoginSilentAsync to get a token using service principal with certificate
            //// ClientAssertionCertificate
            //// ApplicationTokenProvider.LoginSilentWithCertificateAsync

            // Use ApplicationTokenProvider.LoginSilentAsync to get a token using a service principal with symmetric key
            ClientCredential clientCredential = new ClientCredential(config.AadClientId, config.AadSecret);
            return await ApplicationTokenProvider.LoginSilentAsync(config.AadTenantId, clientCredential, ActiveDirectoryServiceSettings.Azure);
        }

        private static async Task<IAzureMediaServicesClient> CreateMediaServicesClientAsync(AzureMediaServicesConfig config)
        {
            var credentials = await GetCredentialsAsync(config);

            return new AzureMediaServicesClient(config.ArmEndpoint, credentials)
            {
                SubscriptionId = config.SubscriptionId,
            };
        }
    }
}

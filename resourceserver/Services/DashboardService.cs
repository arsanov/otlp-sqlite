using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Aspire.DashboardService.Proto.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace AspireResourceServer.Services
{
    public class DashboardService : Aspire.DashboardService.Proto.V1.DashboardService.DashboardServiceBase
    {
        private readonly ILogger<DashboardService> logger;
        private readonly ResourceManager resourceManager;
        private readonly ResourceManagerSettings settings;

        public DashboardService(ILogger<DashboardService> logger,
            ResourceManager resourceManager,
            ResourceManagerSettings settings)
        {
            this.logger = logger;
            this.resourceManager = resourceManager;
            this.settings = settings;
        }

        public override Task<ApplicationInformationResponse> GetApplicationInformation(ApplicationInformationRequest request, ServerCallContext context)
        {
            return Task.FromResult(new ApplicationInformationResponse
            {
                ApplicationName = settings.ServerName ?? "Custom Aspire Resource Server"
            });
        }

        public override async Task WatchResources(WatchResourcesRequest request, IServerStreamWriter<WatchResourcesUpdate> responseStream, ServerCallContext context)
        {
            try
            {
                var initialData = resourceManager.GetInitialResourceData();
                await responseStream.WriteAsync(new WatchResourcesUpdate
                {
                    InitialData = initialData
                });

                await foreach (var changes in resourceManager.WatchChanges(context.CancellationToken))
                {
                    await responseStream.WriteAsync(new WatchResourcesUpdate
                    {
                        Changes = changes
                    });
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("WatchResources cancelled");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in WatchResources");
                throw;
            }
        }

        public override async Task WatchResourceConsoleLogs(WatchResourceConsoleLogsRequest request, IServerStreamWriter<WatchResourceConsoleLogsUpdate> responseStream, ServerCallContext context)
        {
            logger.LogInformation("WatchResourceConsoleLogs called for resource: {ResourceName}", request.ResourceName);

            try
            {
                await foreach (var logUpdate in resourceManager.WatchLogs(request.ResourceName, request.SuppressFollow, context.CancellationToken))
                {
                    await responseStream.WriteAsync(logUpdate);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("WatchResourceConsoleLogs cancelled for resource: {ResourceName}", request.ResourceName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in WatchResourceConsoleLogs for resource: {ResourceName}", request.ResourceName);
                throw;
            }
        }

        public override Task<ResourceCommandResponse> ExecuteResourceCommand(ResourceCommandRequest request, ServerCallContext context)
        {
            try
            {
                var result = resourceManager.ExecuteCommand(request);
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error executing command {CommandName} on resource {ResourceName}", request.CommandName, request.ResourceName);

                return Task.FromResult(new ResourceCommandResponse
                {
                    Kind = ResourceCommandResponseKind.Failed,
                    ErrorMessage = ex.Message
                });
            }
        }

        public override async Task WatchInteractions(IAsyncStreamReader<WatchInteractionsRequestUpdate> requestStream, IServerStreamWriter<WatchInteractionsResponseUpdate> responseStream, ServerCallContext context)
        {
            try
            {
                var readTask = Task.Run(async () =>
                {
                    await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
                    {
                        await resourceManager.ProcessInteractionRequest(request);
                    }
                }, context.CancellationToken);

                var writeTask = Task.Run(async () =>
                {
                    await foreach (var response in resourceManager.WatchInteractions(context.CancellationToken))
                    {
                        await responseStream.WriteAsync(response);
                    }
                }, context.CancellationToken);

                await Task.WhenAll(readTask, writeTask);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("WatchInteractions cancelled");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in WatchInteractions");
                throw;
            }
        }
    }
}
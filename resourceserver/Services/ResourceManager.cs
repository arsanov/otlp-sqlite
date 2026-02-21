using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Aspire.DashboardService.Proto.V1;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OtlpServer;
using OtlpServer.Utils;

namespace AspireResourceServer.Services
{
    public class ResourceManager : IHostedService
    {
        private readonly ILogger<ResourceManager> logger;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly ResourceManagerSettings settings;
        private readonly SingleThreadSynchronizationContext syncContext;
        private readonly HostMapSettings hostMapSettings;
        private readonly IObservable<TraceData> spanStream;
        private readonly ResourceType containerType = new()
        {
            UniqueName = "Container",
            DisplayName = "Container",
            Commands =
            {
                new ResourceCommand
                {
                    Name = "restart",
                    DisplayName = "Restart",
                    State = ResourceCommandState.Enabled,
                    IsHighlighted = false,
                    IconName = "ArrowClockwise"
                }
            }
        };
        private readonly Dictionary<string, Resource> resources = new();
        private readonly ConcurrentDictionary<string, Channel<ConsoleLogLine>> logChannels = new();
        private readonly Channel<WatchResourcesChanges> changeChannel = Channel.CreateUnbounded<WatchResourcesChanges>();
        private readonly Channel<WatchInteractionsResponseUpdate> interactionChannel = Channel.CreateUnbounded<WatchInteractionsResponseUpdate>();
        private int logLineCounter = 0;
        private int interactionIdCounter = 0;
        private Dictionary<EndPoint, string> endpointHostMap;
        private readonly Dictionary<string, ResourceModel> resourceMap = new();
        private readonly List<IDisposable> subscriptions = new();

        public ResourceManager(ILogger<ResourceManager> logger,
         IServiceScopeFactory scopeFactory,
         ResourceManagerSettings settings,
         SingleThreadSynchronizationContext syncContext,
         HostMapSettings hostMapSettings,
         IObservable<TraceData> spanStream)
        {
            this.logger = logger;
            this.scopeFactory = scopeFactory;
            this.settings = settings;
            this.syncContext = syncContext;
            this.hostMapSettings = hostMapSettings;
            this.spanStream = spanStream;
        }

        private static Value DotnetToProtobuf(object node)
        {
            return node switch
            {
                string strValue => new Value { StringValue = strValue },
                double doubleValue => new Value { NumberValue = doubleValue },
                bool boolValue => new Value { BoolValue = boolValue },
                _ => new Value { NullValue = NullValue.NullValue },
            };
        }
        private static object JsonToDotnet(JsonNode node)
        {
            return node switch
            {
                JsonValue value => value.GetValueKind() switch
                {
                    JsonValueKind.String => value.GetValue<string>(),
                    JsonValueKind.Number => value.GetValue<double>(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => null
                },
                _ => null
            };
        }

        private ResourceModel GetResource(string name, string icon = null)
        {
            if (!resourceMap.TryGetValue(name, out var resource))
            {
                resource = ResourceModel.Create(name, icon);
                subscriptions.Add(Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                    handler => handler.Invoke,
                    handler => resource.PropertyChanged += handler,
                    handler => resource.PropertyChanged -= handler)
                    .Select(eventPattern => eventPattern.Sender as ResourceModel)
                    .Select(FromResourceModel).Subscribe(PublishResourceUpdate));
                resourceMap.Add(name, resource);
                PublishResourceUpdate(FromResourceModel(resource));
            }

            return resource;
        }

        private void ProcessResource(TraceModel trace)
        {
            var resource = GetResource(trace.ServiceName, trace.ServiceNamespace == "testing" ? "Beaker" : null);
            var attrs = trace.Attributes;

            resource.Attributes = trace.ResourceAttributes.Where(ra => !resource.Attributes.ContainsKey(ra.Key)).Aggregate(resource.Attributes, (acc, attr) => acc.Add(attr.Key, JsonToDotnet(attr.Value)));

            if (attrs.TryGetValue("server.address", out var saip) && attrs.TryGetValue("server.port", out var sp))
            {
                var ipaddr = saip.GetValue<string>();
                var endpoint = EndpointConverter.ToEndpoint(ipaddr, sp.AsValue().GetValueKind() == JsonValueKind.Number ? sp.GetValue<int>() : Int32.Parse(sp.GetValue<string>()));
                var rn = hostMapSettings.IpMap.TryGetValue(ipaddr) ?? endpointHostMap.TryGetValue(endpoint) ?? EndpointToString(endpoint);

                if (trace.Kind == ActivityKind.Server)
                {
                    var endpointResource = GetResource(rn, "Globe");
                    endpointResource.Parent = resource;
                    if (!resource.Endpoints.ContainsKey(rn))
                    {
                        var scheme = attrs.TryGetValue("url.scheme") ?? "tcp";
                        resource.Endpoints = resource.Endpoints.Add(rn, $"{scheme}://{rn}");
                    }
                }
                else if (trace.Kind == ActivityKind.Producer)
                {
                    if (attrs.TryGetValue("messaging.system", out var ms))
                    {
                        var nr = GetResource(rn, "Mailbox");
                        resource.AddReferenceTo(nr);
                    }
                }
                else if (trace.Kind == ActivityKind.Consumer)
                {
                    if (attrs.TryGetValue("messaging.system", out var ms))
                    {
                        var nr = GetResource(rn, "Mailbox");
                        nr.AddReferenceTo(resource);
                    }
                }
                else if (trace.Kind == ActivityKind.Client)
                {
                    if (attrs.TryGetValue("http.request.method", out var ms))
                    {
                        var nr = GetResource(rn, "Globe");
                        resource.AddReferenceTo(nr);
                    }
                    else if (attrs.TryGetValue("db.system", out var ds))
                    {
                        var nr = GetResource(rn, "Database");
                        resource.AddReferenceTo(nr);
                    }
                }
            }
        }
        private Resource FromResourceModel(ResourceModel resourceModel)
        {
            var resource = new Resource
            {
                Name = resourceModel.Name,
                DisplayName = resourceModel.Name,
                ResourceType = "Container",
                Uid = resourceModel.Guid.ToString(),
                State = "Running",
                StateStyle = "success",
                CreatedAt = Timestamp.FromDateTime(DateTime.UtcNow),
                StartedAt = Timestamp.FromDateTime(DateTime.UtcNow),
                SupportsDetailedTelemetry = true,
                IconName = resourceModel.IconName,
            };
            foreach (var ppair in resourceModel.Attributes)
            {
                resource.Properties.Add(new ResourceProperty
                {
                    DisplayName = ppair.Key,
                    Name = ppair.Key,
                    Value = DotnetToProtobuf(ppair.Value)
                });
            }
            var i = 0;
            foreach (var epair in resourceModel.Endpoints)
            {
                resource.Urls.Add(new Url
                {
                    EndpointName = epair.Key,
                    FullUrl = epair.Value,
                    DisplayProperties = new UrlDisplayProperties
                    {
                        DisplayName = epair.Key,
                        SortOrder = ++i
                    }
                });
            }

            foreach (var epair in resourceModel.EnvironmentVariables)
            {
                resource.Environment.Add(new EnvironmentVariable
                {
                    Name = epair.Key,
                    Value = epair.Value
                });
            }

            foreach (var pair in resourceModel.References)
            {
                resource.Relationships.Add(new ResourceRelationship
                {
                    ResourceName = pair.Value.Name,
                    Type = KnownRelationshipTypes.Reference
                });
            }

            if (resourceModel.Parent != null)
            {
                resource.Relationships.Add(new ResourceRelationship
                {
                    ResourceName = resourceModel.Parent.Name,
                    Type = KnownRelationshipTypes.Parent
                });
            }

            return resource;
        }

        // private async Task InitializeSampleResources(DatabaseContext context)
        // {
        //     var timestamp = ((ulong)DateTimeOffset.UtcNow.Add(settings.DbTraceSince).ToUnixTimeMilliseconds()) * 1000000;
        //     var entries = context.TraceEntries.AsEnumerable().Where(e => e.StartTimeUnixNano > timestamp).Select(TraceModel.FromDb).ToList();
        //     foreach (var trace in entries)
        //     {
        //         ProcessResource(trace);
        //     }
        // }

        public InitialResourceData GetInitialResourceData()
        {
            var initialData = new InitialResourceData();
            initialData.Resources.AddRange(resourceMap.Values.Select(FromResourceModel));
            initialData.ResourceTypes.Add(containerType);
            return initialData;
        }

        public async IAsyncEnumerable<WatchResourcesChanges> WatchChanges([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (await changeChannel.Reader.WaitToReadAsync(cancellationToken))
            {
                while (changeChannel.Reader.TryRead(out var changes))
                {
                    yield return changes;
                }
            }
        }

        public async IAsyncEnumerable<WatchResourceConsoleLogsUpdate> WatchLogs(string resourceName, bool suppressFollow, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var logChannel = logChannels.GetOrAdd(resourceName, _ => Channel.CreateUnbounded<ConsoleLogLine>());

            var initialLogs = new WatchResourceConsoleLogsUpdate();
            initialLogs.LogLines.Add(new ConsoleLogLine
            {
                Text = $"[{DateTime.UtcNow:HH:mm:ss}] Starting resource: {resourceName}",
                IsStdErr = false,
                LineNumber = Interlocked.Increment(ref logLineCounter)
            });
            initialLogs.LogLines.Add(new ConsoleLogLine
            {
                Text = $"[{DateTime.UtcNow:HH:mm:ss}] Resource {resourceName} is running",
                IsStdErr = false,
                LineNumber = Interlocked.Increment(ref logLineCounter)
            });
            yield return initialLogs;

            if (suppressFollow)
            {
                yield break;
            }

            // Continue streaming logs
            while (await logChannel.Reader.WaitToReadAsync(cancellationToken))
            {
                var update = new WatchResourceConsoleLogsUpdate();
                while (logChannel.Reader.TryRead(out var logLine))
                {
                    update.LogLines.Add(logLine);
                }
                if (update.LogLines.Count > 0)
                {
                    yield return update;
                }
            }
        }

        public ResourceCommandResponse ExecuteCommand(ResourceCommandRequest request)
        {
            logger.LogInformation("Executing command {Command} on resource {Resource} of type {Type}",
                request.CommandName,
                request.ResourceName,
                request.ResourceType);

            if (!resources.TryGetValue(request.ResourceName, out var resource))
            {
                return new ResourceCommandResponse
                {
                    Kind = ResourceCommandResponseKind.Failed,
                    ErrorMessage = $"Resource '{request.ResourceName}' not found"
                };
            }

            // Simulate command execution
            switch (request.CommandName.ToLowerInvariant())
            {
                case "start":
                    resource.State = "Running";
                    resource.StateStyle = "success";
                    resource.StartedAt = Timestamp.FromDateTime(DateTime.UtcNow);
                    PublishResourceUpdate(resource);
                    break;

                case "stop":
                    resource.State = "Stopped";
                    resource.StateStyle = "info";
                    resource.StoppedAt = Timestamp.FromDateTime(DateTime.UtcNow);
                    PublishResourceUpdate(resource);
                    break;

                case "restart":
                    resource.State = "Restarting";
                    resource.StateStyle = "warning";
                    PublishResourceUpdate(resource);

                    // Simulate restart delay
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(2000);
                        resource.State = "Running";
                        resource.StateStyle = "success";
                        resource.StartedAt = Timestamp.FromDateTime(DateTime.UtcNow);
                        PublishResourceUpdate(resource);
                    });
                    break;

                default:
                    return new ResourceCommandResponse
                    {
                        Kind = ResourceCommandResponseKind.Failed,
                        ErrorMessage = $"Unknown command: {request.CommandName}"
                    };
            }

            return new ResourceCommandResponse
            {
                Kind = ResourceCommandResponseKind.Succeeded
            };
        }

        private void PublishResourceUpdate(Resource resource)
        {
            var changes = new WatchResourcesChanges();
            changes.Value.Add(new WatchResourcesChange
            {
                Upsert = resource,
            });
            changeChannel.Writer.TryWrite(changes);
        }

        public void AddLogLine(string resourceName, string text, bool isStdErr = false)
        {
            var logChannel = logChannels.GetOrAdd(resourceName, _ => Channel.CreateUnbounded<ConsoleLogLine>());
            logChannel.Writer.TryWrite(new ConsoleLogLine
            {
                Text = text,
                IsStdErr = isStdErr,
                LineNumber = Interlocked.Increment(ref logLineCounter)
            });
        }

        public async IAsyncEnumerable<WatchInteractionsResponseUpdate> WatchInteractions([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (await interactionChannel.Reader.WaitToReadAsync(cancellationToken))
            {
                while (interactionChannel.Reader.TryRead(out var interaction))
                {
                    yield return interaction;
                }
            }
        }

        public Task ProcessInteractionRequest(WatchInteractionsRequestUpdate request)
        {
            logger.LogInformation("Processing interaction request {InteractionId}", request.InteractionId);

            if (request.Complete != null)
            {
                logger.LogInformation("Interaction {InteractionId} completed", request.InteractionId);
            }
            else if (request.MessageBox != null)
            {
                logger.LogInformation("Message box response: {Result}", request.MessageBox.Result);
            }
            else if (request.InputsDialog != null)
            {
                logger.LogInformation("Inputs dialog with {Count} inputs", request.InputsDialog.InputItems.Count);
            }

            return Task.CompletedTask;
        }

        public void SendInteraction(WatchInteractionsResponseUpdate interaction)
        {
            interactionChannel.Writer.TryWrite(interaction);
        }

        public int GetNextInteractionId() => Interlocked.Increment(ref interactionIdCounter);

        private static string EndpointToString(EndPoint endPoint)
        {
            return endPoint switch
            {
                IPEndPoint ie => $"{ie.Address}:{ie.Port}",
                DnsEndPoint ie => $"{ie.Host}:{ie.Port}",
                _ => "unknown-endpoint"
            };
        }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            endpointHostMap = hostMapSettings.Hosts?.SelectMany(g => g.Select(i => (i, EndpointToString(g[0])))).ToDictionary((t) => t.i, t => t.Item2) ?? [];
            // if (settings.LoadFromDb)
            // {
            //     using var scope = scopeFactory.CreateScope();

            //     await InitializeSampleResources(scope.ServiceProvider.GetRequiredService<DatabaseContext>());
            // }
            _ = syncContext.ContextStarted.ContinueWith(t =>
            {
                subscriptions.Add(spanStream.ObserveOn(syncContext).Subscribe(UpdateReource));
            });
            return Task.CompletedTask;
        }

        private void UpdateReource(TraceData data)
        {
            ProcessResource(TraceModel.FromProtobuf(data));
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var subscription in subscriptions)
            {
                subscription.Dispose();
            }
            return Task.CompletedTask;
        }
    }
}
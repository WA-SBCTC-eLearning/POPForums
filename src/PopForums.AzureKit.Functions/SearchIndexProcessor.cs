using System;
using System.Diagnostics;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PopForums.AzureKit.Queue;
using PopForums.Configuration;
using PopForums.Extensions;
using PopForums.Messaging;
using PopForums.Models;
using PopForums.Services;
using PopForums.Sql;

namespace PopForums.AzureKit.Functions
{
	public static class SearchIndexProcessor
	{
		[FunctionName("SearchIndexProcessor")]
		public static void Run([QueueTrigger(SearchIndexQueueRepository.QueueName)]
			string jsonPayload, ILogger log, ExecutionContext context)
		{
			var stopwatch = new Stopwatch();
			stopwatch.Start();
			Config.SetPopForumsAppEnvironment(context.FunctionAppDirectory, "local.settings.json");
			var services = new ServiceCollection();
			services.AddPopForumsBase();
			services.AddPopForumsSql();
			services.AddPopForumsAzureFunctionsAndQueues();
			services.AddTransient<IBroker, BrokerSink>();

			var serviceProvider = services.BuildServiceProvider();
			var searchIndexSubsystem = serviceProvider.GetService<ISearchIndexSubsystem>();
			var serviceHeartbeatService = serviceProvider.GetService<IServiceHeartbeatService>();
			var errorLog = serviceProvider.GetService<IErrorLog>();

			try
			{
				var payload = JsonConvert.DeserializeObject<SearchIndexPayload>(jsonPayload);
				searchIndexSubsystem.DoIndex(payload.TopicID, payload.TenantID);
			}
			catch (Exception exc)
			{
				errorLog.Log(exc, ErrorSeverity.Error);
			}

			stopwatch.Stop();
			log.LogInformation($"C# Queue AwardCalculationProcessor function processed ({stopwatch.ElapsedMilliseconds}ms): {jsonPayload}");
			serviceHeartbeatService.RecordHeartbeat(typeof(SearchIndexProcessor).FullName, "AzureFunction");
		}
	}
}
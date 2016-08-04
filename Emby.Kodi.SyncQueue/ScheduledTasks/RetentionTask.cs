﻿using System.Globalization;
using MediaBrowser.Common.Net;
using MediaBrowser.Common.ScheduledTasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Common.Configuration;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Emby.Kodi.SyncQueue.Data;

namespace Emby.Kodi.SyncQueue.ScheduledTasks
{
    public class FireRetentionTask : IScheduledTask
    {
        //private readonly IHttpClient _httpClient;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IUserManager _userManager;
        private readonly ILogger _logger;
        private readonly ILogManager _logManager;
        private readonly IUserDataManager _userDataManager;
        private readonly IApplicationPaths _applicationPaths;        

        public FireRetentionTask(ILogManager logManager, ILogger logger, IJsonSerializer jsonSerializer, IUserManager userManager, 
            IUserDataManager userDataManager, IHttpClient httpClient, IServerApplicationHost appHost, IApplicationPaths applicationPaths)
        {
            _jsonSerializer = jsonSerializer;
            _userManager = userManager;
            _userDataManager = userDataManager;
            _logger = logger;
            _logManager = logManager;
            _applicationPaths = applicationPaths;

            _logger.Info("Emby.Kodi.SyncQueue.Task: Retention Task Scheduled!");
        }

        public IEnumerable<ITaskTrigger> GetDefaultTriggers()
        {

            //
            return new ITaskTrigger[]
            {
                new DailyTrigger{ TimeOfDay = TimeSpan.FromMinutes(1) }
            };
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            //Is retDays 0.. If So Exit...
            int retDays;

            if (!(Int32.TryParse(Plugin.Instance.Configuration.RetDays, out retDays))) {
                _logger.Info("Emby.Kodi.SyncQueue.Task: Retention Deletion Not Possible When Retention Days = 0!");
                return;
            }

            if (retDays == 0)
            {
                _logger.Info("Emby.Kodi.SyncQueue.Task: Retention Deletion Not Possible When Retention Days = 0!");
                return;
            }

            //Check Database
            bool result = await Task.Run(() =>
            {
                retDays = retDays * -1;
                var dt = DateTime.UtcNow.AddDays(retDays);
                var dtl = (long)(dt.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
                //DbRepo.DeleteOldData(dtl, _logger);

                using (var repo = new DbRepo(_applicationPaths.DataPath, _logger))
                {                    
                    repo.DeleteOldData(dtl);
                }

                    return true;
            });
        }

        public string Name
        {
            get { return "Remove Old Sync Data"; }
        }

        public string Category
        {
            get
            {
                return "Emby.Kodi.SyncQueue";
            }
        }

        public string Description
        {
            get
            {
                return
                    "If Retention Days > 0 then this will remove the old data to keep information flowing quickly";
            }
        }
    }

}

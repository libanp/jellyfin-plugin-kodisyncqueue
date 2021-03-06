﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Serialization;
using Jellyfin.Plugin.KodiSyncQueue.Entities;
using Jellyfin.Plugin.KodiSyncQueue.Data;
using System.Globalization;
using Jellyfin.Plugin.KodiSyncQueue.Utils;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Services;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.KodiSyncQueue.API
{
    public class SyncAPI : IService
    {
        private readonly ILogger _logger;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IUserManager _userManager;
        private readonly ILibraryManager _libraryManager;

        public SyncAPI(ILogger logger, IJsonSerializer jsonSerializer, IUserManager userManager, ILibraryManager libraryManager)
        {
            _logger = logger;
            _jsonSerializer = jsonSerializer;
            _userManager = userManager;
            _libraryManager = libraryManager;

            _logger.LogInformation("SyncAPI Created and Listening at \"/Jellyfin.Plugin.KodiSyncQueue/{UserID}/{LastUpdateDT}/GetItems?format=json\" - {LastUpdateDT} must be a UTC DateTime formatted as yyyy-MM-ddTHH:mm:ssZ");
            _logger.LogInformation("SyncAPI Created and Listening at \"/Jellyfin.Plugin.KodiSyncQueue/{UserID}/GetItems?LastUpdateDT={LastUpdateDT}&format=json\" - {LastUpdateDT} must be a UTC DateTime formatted as yyyy-MM-ddTHH:mm:ssZ");
            _logger.LogInformation("The following parameters also exist to filter the results:");
            _logger.LogInformation("filter=movies,tvshows,music,musicvideos,boxsets");
            _logger.LogInformation("Results will be included by default and only filtered if added to the filter query...");
            _logger.LogInformation("the filter query must be lowercase in both the name and the items...");
        }

        public async Task<SyncUpdateInfo> Get(GetLibraryItemsQuery request)
        {
            _logger.LogInformation("Sync Requested for UserID: '{UserId}' with LastUpdateDT: '{LastUpdateDT}'", request.UserID, request.LastUpdateDT);
            _logger.LogDebug("Processing message...");
            if (string.IsNullOrEmpty(request.LastUpdateDT))
                request.LastUpdateDT = "1900-01-01T00:00:00Z";
            bool movies = true;
            bool tvshows = true;
            bool music = true;
            bool musicvideos = true;
            bool boxsets = true;

            if (!string.IsNullOrEmpty(request.filter))
            {
                var filter = request.filter.ToLower().Split(',');
                foreach (var f in filter)
                {
                    f.Trim();
                    switch (f)
                    {
                        case "movies":
                            movies = false;
                            break;
                        case "tvshows":
                            tvshows = false;
                            break;
                        case "music":
                            music = false;
                            break;
                        case "musicvideos":
                            musicvideos = false;
                            break;
                        case "boxsets":
                            boxsets = false;
                            break;
                    }
                }
            }

            return await PopulateLibraryInfo(
                request.UserID,
                request.LastUpdateDT,
                movies,
                tvshows,
                music,
                musicvideos,
                boxsets
            );
        }        

        public async Task<SyncUpdateInfo> PopulateLibraryInfo(string userId, string lastDT, 
                                                              bool movies, bool tvshows, bool music,
                                                              bool musicvideos, bool boxsets)
        {
            var startTime = DateTime.UtcNow;

            _logger.LogDebug("Starting PopulateLibraryInfo...");
            var userDataChangedJson = new List<string>();

            var info = new SyncUpdateInfo();

            var userDT = DateTime.Parse(lastDT, CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal);

            var dtl = (long)userDT.ToUniversalTime().Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

            Task<List<string>> t1 = Task.Run(() =>
            {
                _logger.LogDebug("PopulateLibraryInfo:  Getting Items Added Info...");
                var data = DbRepo.Instance.GetItems(dtl, 0, movies, tvshows, music, musicvideos, boxsets);
                
                var user = _userManager.GetUserById(Guid.Parse(userId));

                List<BaseItem> items = new List<BaseItem>();
                data.ForEach(i =>
                {
                    var item = _libraryManager.GetItemById(i);
                    if (item != null)
                    {
                        items.Add(item);
                    }
                });

                var result = items.SelectMany(i => ApiUserCheck.TranslatePhysicalItemToUserLibrary(i, user, _libraryManager)).Select(i => i.Id.ToString("N")).Distinct().ToList();
                
                if (result.Count > 0)
                {
                    _logger.LogInformation("Added Items Found: {AddedItems}", string.Join(",", result.ToArray()));
                }
                else
                {
                    _logger.LogInformation("No Added Items Found!");
                }
                return result;
            });

            Task<List<string>> t2 = Task.Run(() =>
            {
                _logger.LogDebug("PopulateLibraryInfo:  Getting Items Removed Info...");
                List<string> result = new List<string>();

                var data = DbRepo.Instance.GetItems(dtl, 2, movies, tvshows, music, musicvideos, boxsets);

                if (data != null && data.Any())
                {
                    data.ForEach(i => result.Add(i.ToString("N")));
                }

                if (result.Count > 0)
                {
                    _logger.LogInformation("Removed Items Found: {RemovedItems}", string.Join(",", result.ToArray()));
                }
                else
                {
                    _logger.LogInformation("No Removed Items Found!");
                }
                return result;
            });

            Task<List<string>> t3 = Task.Run(() =>
            {
                _logger.LogDebug("PopulateLibraryInfo:  Getting Items Updated Info...");
                var data = DbRepo.Instance.GetItems(dtl, 1, movies, tvshows, music, musicvideos, boxsets);
                
                var user = _userManager.GetUserById(Guid.Parse(userId));

                List<BaseItem> items = new List<BaseItem>();
                data.ForEach(i =>
                {
                    var item = _libraryManager.GetItemById(i);
                    if (item != null)
                    {
                        items.Add(item);
                    }
                });

                var result = items.SelectMany(i => ApiUserCheck.TranslatePhysicalItemToUserLibrary(i, user, _libraryManager)).Select(i => i.Id.ToString("N")).Distinct().ToList();

                if (result.Count > 0)
                {
                    _logger.LogInformation("Updated Items Found: {UpdatedItems}", string.Join(",", result));
                }
                else
                {
                    _logger.LogInformation("No Updated Items Found!");
                }
                return result;
            });

            Task<List<string>> t4 = Task.Run(() =>
            {
                _logger.LogDebug("PopulateLibraryInfo:  Getting User Data Changed Info...");
                var data = DbRepo.Instance.GetUserInfos(dtl, userId, movies, tvshows, music, musicvideos, boxsets);
                
                var result = data.Select(i => i.JsonData).ToList();

                if (result.Count > 0)
                {
                    _logger.LogInformation("User Data Changed Info Found: {ChangedData}", string.Join(",", data.Select(i => i.Id).ToArray()));
                }
                else
                {
                    _logger.LogInformation("No User Data Changed Info Found!");
                }
                
                return result;
            });

            await Task.WhenAll(t1, t2, t3, t4);

            info.ItemsAdded = t1.Result;
            info.ItemsRemoved = t2.Result;
            info.ItemsUpdated = t3.Result;
            userDataChangedJson = t4.Result;

            info.UserDataChanged = userDataChangedJson.Select(i => _jsonSerializer.DeserializeFromString<UserItemDataDto>(i)).ToList();

            TimeSpan diffDate = DateTime.UtcNow - startTime;
            _logger.LogInformation("Request Finished Taking {TimeTaken}", diffDate.ToString("c"));

            return info;
        }
    }
}

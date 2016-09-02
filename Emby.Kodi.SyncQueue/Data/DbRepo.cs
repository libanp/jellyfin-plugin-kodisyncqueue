﻿using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System.IO;
using LiteDB;
using Emby.Kodi.SyncQueue.Entities;

namespace Emby.Kodi.SyncQueue.Data
{
    public class DbRepo: IDisposable
    {
        private object _createLock = new object();
        private LiteDatabase DB = null;
        private string dataPath;
        private string dataName = "Emby.Kodi.SyncQueue.1.2.ldb";

        private readonly ILogger _logger;
        private readonly IJsonSerializer _json;

        private LiteCollection<FolderRec> folders = null;
        private LiteCollection<ItemRec> items = null;
        private LiteCollection<UserInfoRec> userinfos = null;
        
        public string DataPath
        {
            get { return dataPath; }
            set { dataPath = Path.Combine(value, "SyncData"); }
        }

        public DbRepo(string dp, ILogger logger, IJsonSerializer json = null)
        {
            DataPath = dp;
            var data = Path.Combine(DataPath, dataName);
            //var newdb = false;
            _logger = logger;
            _json = json;

            if (!Directory.Exists(DataPath))
            {
                Directory.CreateDirectory(DataPath);
            }

            if (!File.Exists(data))
            {
            //    newdb = true;
            }
            if (DB == null) { DB = new LiteDatabase(data); }
            
            folders = DB.GetCollection<FolderRec>("Folders");
            items = DB.GetCollection<ItemRec>("Items");
            userinfos = DB.GetCollection<UserInfoRec>("UserInfos");

            folders.EnsureIndex(x => x.ItemId);
            folders.EnsureIndex(x => x.UserId);
            folders.EnsureIndex(x => x.LastModified);
            folders.EnsureIndex(x => x.Status);
            folders.EnsureIndex(x => x.MediaType);
            items.EnsureIndex(x => x.ItemId);
            items.EnsureIndex(x => x.UserId);
            items.EnsureIndex(x => x.LastModified);
            items.EnsureIndex(x => x.Status);
            items.EnsureIndex(x => x.MediaType);
            userinfos.EnsureIndex(x => x.ItemId);
            userinfos.EnsureIndex(x => x.UserId);
            userinfos.EnsureIndex(x => x.LastModified);
            userinfos.EnsureIndex(x => x.MediaType);
        }      



        public List<string> GetItems(long dtl, int status, string userId, bool movies, bool tvshows, bool music, bool musicvideos, bool boxsets)
        {
            using (var trn = DB.BeginTrans())
            {
                var result = new List<string>();
                List<ItemRec> final = new List<ItemRec>();
                //
                _logger.Debug(String.Format("Emby.Kodi.SyncQueue:  Using dtl {0:yyyy-MM-dd HH:mm:ss} for time {1}", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(dtl), dtl));
                _logger.Debug(String.Format("Emby.Kodi.SyncQueue:  IntStatus: {0}", status));
                _logger.Debug(String.Format("Emby.Kodi.SyncQueue:  userId: {0}", userId));

                var itms = items.Find(x => x.LastModified > dtl &&
                                           x.Status == status &&
                                           x.UserId == userId).ToList();
                itms = itms.Where(x =>
                                {
                                    switch (x.MediaType)
                                    {
                                        case 0:
                                            if (movies) { return true; } else { return false; }
                                        case 1:
                                            if (tvshows) { return true; } else { return false; }
                                        case 2:
                                            if (music) { return true; } else { return false; }
                                        case 3:
                                            if (musicvideos) { return true; } else { return false; }
                                        case 4:
                                            if (boxsets) { return true; } else { return false; }
                                    }
                                    return false;
                                })
                                .ToList();

                //if (movies) { final.AddRange(itms.Where(x => x.MediaType == "movies").ToList()); }
                //if (tvshows) { final.AddRange(itms.Where(x => x.MediaType == "tvshows").ToList()); }
                //if (music) { final.AddRange(itms.Where(x => x.MediaType == "music").ToList()); }
                //if (musicvideos) { final.AddRange(itms.Where(x => x.MediaType == "musicvideos").ToList()); }
                //if (boxsets) { final.AddRange(itms.Where(x => x.MediaType == "boxsets").ToList()); }

                //final.ForEach(x =>
                itms.ForEach(x =>
                {
                    if (result.Where(i => i == x.ItemId).FirstOrDefault() == null)
                    {
                        _logger.Debug(String.Format("Emby.Kodi.SyncQueue:  Item {0} Modified {1:yyyy-MM-dd HH:mm:ss} for time {2}", x.ItemId, 
                                new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(x.LastModified), x.LastModified));
                        result.Add(x.ItemId);
                    }
                });

                //var temp = items.FindAll().ToList();
                //temp.ForEach(x =>
                //{
                //    _logger.Info(String.Format("Emby.Kodi.SyncQueue:  Found item: {0}", _json.SerializeToString(x)));
                //});

                return result;
            }            
        }

        public List<string> GetFolders(long dtl, int status, string userId, bool movies, bool tvshows, bool music, bool musicvideos, bool boxsets)
        {
            using (var trn = DB.BeginTrans())
            {
                var result = new List<string>();
                List<FolderRec> final = new List<FolderRec>();
                //
                var flds = folders.Find(x => x.LastModified > dtl && 
                                             x.Status == status && 
                                             x.UserId == userId)
                                  .Where(x =>
                                  {
                                      switch (x.MediaType)
                                      {
                                          case "movies":
                                              if (movies) { return true; } else { return false; }
                                          case "tvshows":
                                              if (tvshows) { return true; } else { return false; }
                                          case "music":
                                              if (music) { return true; } else { return false; }
                                          case "musicvideos":
                                              if (musicvideos) { return true; } else { return false; }
                                          case "boxsets":
                                              if (boxsets) { return true; } else { return false; }
                                      }
                                      return false;
                                  })
                                  .ToList();

                //if (movies) { final.AddRange(flds.Where(x => x.MediaType == "movies").ToList()); }
                //if (tvshows) { final.AddRange(flds.Where(x => x.MediaType == "tvshows").ToList()); }
                //if (music) { final.AddRange(flds.Where(x => x.MediaType == "music").ToList()); }
                //if (musicvideos) { final.AddRange(flds.Where(x => x.MediaType == "musicvideos").ToList()); }
                //if (boxsets) { final.AddRange(flds.Where(x => x.MediaType == "boxsets").ToList()); }

                //final.ForEach(x =>
                flds.ForEach(x =>
                {
                    if (result.Where(i => i == x.ItemId).FirstOrDefault() == null)
                    {
                        result.Add(x.ItemId);
                    }
                });
                return result;
            }
        }

        public List<string> GetUserInfos(long dtl, string userId, out List<string> ids, bool movies, bool tvshows, bool music, bool musicvideos, bool boxsets)
        {
            using (var trn = DB.BeginTrans())
            {
                var result = new List<string>();
                var tids = new List<string>();
                var final = new List<UserInfoRec>();

                var uids = userinfos.Find(x => x.LastModified > dtl &&
                                               x.UserId == userId).ToList();
                uids = uids.Where(x =>
                            {
                                switch (x.MediaType)
                                {
                                    case 0:
                                        if (movies) { return true; } else { return false; }
                                    case 1:
                                        if (tvshows) { return true; } else { return false; }
                                    case 2:
                                        if (music) { return true; } else { return false; }
                                    case 3:
                                        if (musicvideos) { return true; } else { return false; }
                                    case 4:
                                        if (boxsets) { return true; } else { return false; }
                                }
                                return false;
                            })
                            .ToList();

                //if (movies) { final.AddRange(uids.Where(x => x.MediaType == "movies").ToList()); }
                //if (tvshows) { final.AddRange(uids.Where(x => x.MediaType == "tvshows").ToList()); }
                //if (music) { final.AddRange(uids.Where(x => x.MediaType == "music").ToList()); }
                //if (musicvideos) { final.AddRange(uids.Where(x => x.MediaType == "musicvideos").ToList()); }
                //if (boxsets) { final.AddRange(uids.Where(x => x.MediaType == "boxsets").ToList()); }

                //final.ForEach(x =>
                uids.ForEach(x =>
                {
                    result.Add(x.Json);
                    tids.Add(x.ItemId);
                });
                ids = tids;

                //var temp = items.FindAll();
                //foreach (var t in temp)
                //{
                //    _logger.Debug("");
                //    _logger.Debug(String.Format("Emby.Kodi.SyncQueue:  Id:        {0}", t.Id));
                //    _logger.Debug(String.Format("Emby.Kodi.SyncQueue:  ItemId:    {0}", t.ItemId));
                //    _logger.Debug(String.Format("Emby.Kodi.SyncQueue:  UserId:    {0}", t.UserId));
                //    _logger.Debug(String.Format("Emby.Kodi.SyncQueue:  MediaType: {0}", t.MediaType));
                //    //_logger.Debug(String.Format("Emby.Kodi.SyncQueue:  CollName:  {0}", t.LibraryName));
                //    _logger.Debug(String.Format("Emby.Kodi.SyncQueue:  Status:    {0}", t.Status));
                //    _logger.Debug(String.Format("Emby.Kodi.SyncQueue:  LastMod:   {0}", t.LastModified));
                //};

                //var temp2 = userinfos.FindAll();
                //foreach (var t in temp2)
                //{
                //    _logger.Debug("");
                //    _logger.Debug(String.Format("Emby.Kodi.SyncQueue:  Id:        {0}", t.Id));
                //    _logger.Debug(String.Format("Emby.Kodi.SyncQueue:  ItemId:    {0}", t.ItemId));
                //    _logger.Debug(String.Format("Emby.Kodi.SyncQueue:  UserId:    {0}", t.UserId));
                //    _logger.Debug(String.Format("Emby.Kodi.SyncQueue:  MediaType: {0}", t.MediaType));
                //    //_logger.Debug(String.Format("Emby.Kodi.SyncQueue:  CollName:  {0}", t.LibraryName));
                //    _logger.Debug(String.Format("Emby.Kodi.SyncQueue:  JSON:      {0}", t.Json));
                //    _logger.Debug(String.Format("Emby.Kodi.SyncQueue:  LastMod:   {0}", t.LastModified));
                //};
                return result;
            }
        }

        public void DeleteOldData(long dtl)
        {
            using (var trn = DB.BeginTrans())
            {
                try
                { 
                    _logger.Info("Emby.Kodi.SyncQueue.Task: Starting Folder Retention Deletion...");
                    folders.Delete(x => x.LastModified < dtl);
                    _logger.Info("Emby.Kodi.SyncQueue.Task: Finished Folder Retention Deletion...");

                    _logger.Info("Emby.Kodi.SyncQueue.Task: Starting Item Retention Deletion...");
                    items.Delete(x => x.LastModified < dtl);
                    _logger.Info("Emby.Kodi.SyncQueue.Task: Finished Item Retention Deletion...");

                    _logger.Info("Emby.Kodi.SyncQueue.Task: Starting UserItem Retention Deletion...");
                    userinfos.Delete(x => x.LastModified < dtl);
                    _logger.Info("Emby.Kodi.SyncQueue.Task: Finished UserItem Retention Deletion...");

                    trn.Commit();
                }
                catch (Exception)
                {               
                    trn.Rollback();
                    throw;
                }
            }
        }

        public void SetLibrarySync(List<string> sItems, List<LibItem> libItems, string userId, string userName, int status, CancellationToken cancellationToken)
        {
            ItemRec newRec;
            var statusType = string.Empty;
            if (status == 0) { statusType = "Added"; }
            else if (status == 1) { statusType = "Updated"; }
            else { statusType = "Removed"; }
            using (var trn = DB.BeginTrans())
            {
                try
                {
                    sItems.ForEach(i =>
                    {
                        
                        var libitem = libItems.Where(itm => itm.Id.ToString("N") == i).FirstOrDefault();
                        long newTime;

                        if (libitem != null)
                        {
                            newTime = libitem.SyncApiModified;

                            ItemRec rec = items.Find(x => x.ItemId == i && x.UserId == userId).FirstOrDefault();

                            newRec = new ItemRec()
                            {
                                ItemId = i,
                                UserId = userId,
                                Status = status,
                                LastModified = newTime,
                                MediaType = libitem.ItemType
                                //LibraryName = libitem.CollectionName
                            };

                            if (rec == null) { items.Insert(newRec); }
                            else if (rec.LastModified < newTime)
                            {
                                newRec.Id = rec.Id;
                                items.Update(newRec);
                            }
                            else { newRec = null; }

                            if (newRec != null)
                            {
                                _logger.Debug(String.Format("Emby.Kodi.SyncQueue:  {0} ItemId: '{1}' for UserId: '{2}'", statusType, newRec.ItemId, newRec.UserId));
                            }
                            else
                            {
                                _logger.Debug(String.Format("Emby.Kodi.SyncQueue:  ItemId: '{0}' Skipped for UserId: '{1}'", i, userId));
                            }
                        }                     
                    });
                    trn.Commit();
                }
                catch (Exception ex)
                {
                    trn.Rollback();
                    throw ex;
                }
            }
        }

        public void SetUserInfoSync(List<MediaBrowser.Model.Dto.UserItemDataDto> dtos, List<LibItem> itemRefs, string userName, string userId, CancellationToken cancellationToken)
        {
            using (var trn = DB.BeginTrans())
            {
                try
                {
                    dtos.ForEach(dto =>
                    {
                        var json = _json.SerializeToString(dto).ToString();
                        _logger.Debug("Emby.Kodi.SyncQueue:  Updating ItemId '{0}' for UserId: '{1}'", dto.ItemId, userId);

                        LibItem itemref = itemRefs.Where(x => x.Id.ToString("N") == dto.ItemId).FirstOrDefault();
                        if (itemref != null)
                        {
                            UserInfoRec oldRec = userinfos.Find(x => x.ItemId == dto.ItemId && x.UserId == userId)
                                                          .FirstOrDefault();
                            var newRec = new UserInfoRec()
                            {
                                ItemId = dto.ItemId,
                                Json = json,
                                UserId = userId,
                                LastModified = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds),
                                MediaType = itemref.ItemType,
                                //LibraryName = itemref.CollectionName
                            };
                            if (oldRec == null)
                            {
                                userinfos.Insert(newRec);
                            }
                            else
                            {
                                newRec.Id = oldRec.Id;
                                userinfos.Update(newRec);

                            }
                        }
                    });
                    trn.Commit();
                }
                catch (Exception)
                {
                    trn.Rollback();
                    throw;
                }
            }
        }

        #region Dispose

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool dispose)
        {
            if (dispose)
            {
                if (DB != null) { DB.Dispose(); }
            }
        }

        #endregion
    }
}

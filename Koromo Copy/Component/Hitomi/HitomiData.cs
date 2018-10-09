﻿/***

   Copyright (C) 2018. dc-koromo. All Rights Reserved.
   
   Author: Koromo Copy Developer

***/

using Koromo_Copy.Interface;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Koromo_Copy.Component.Hitomi
{
    /// <summary>
    /// 히토미 데이터베이스를 총괄하는 클래스입니다.
    /// </summary>
    public class HitomiData : ILazy<HitomiData>
    {
        public const int max_number_of_result = 10;
        public const int number_of_gallery_jsons = 20;
        
        public static string tag_json_uri = @"https://ltn.hitomi.la/tags.json";
        public static string gallerie_json_uri(int no) => $"https://ltn.hitomi.la/galleries{no}.json";

        public static string hidden_data_url = @"https://github.com/dc-koromo/hitomi-downloader-2/releases/download/hiddendata/hiddendata.json";

        public HitomiTagdataCollection tagdata_collection = new HitomiTagdataCollection();
        public List<HitomiMetadata> metadata_collection;
        public Dictionary<string, string> thumbnail_collection;

        #region Metadata
        public int downloadCount = 0;
        public object lock_count = new object();

        public async Task DownloadMetadata()
        {
            Monitor.Instance.Push("Download Metadata...");
            ServicePointManager.DefaultConnectionLimit = 999999999;
            metadata_collection = new List<HitomiMetadata>();
            downloadCount = 0;
            await Task.WhenAll(Enumerable.Range(0, number_of_gallery_jsons).Select(no => downloadMetadata(no)));
            SortMetadata();

            if (!Settings.Instance.Hitomi.AutoSync)
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Converters.Add(new JavaScriptDateTimeConverter());
                serializer.NullValueHandling = NullValueHandling.Ignore;

                Monitor.Instance.Push("Write file: metadata.json");
                using (StreamWriter sw = new StreamWriter(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "metadata.json")))
                using (JsonWriter writer = new JsonTextWriter(sw))
                {
                    serializer.Serialize(writer, metadata_collection);
                }
            }
            return;
        }

        public async Task DownloadHiddendata()
        {
            Monitor.Instance.Push("Download Hiddendata...");
            thumbnail_collection = new Dictionary<string, string>();
            HttpClient client = new HttpClient();
            client.Timeout = new TimeSpan(0, 0, 0, 0, Timeout.Infinite);
            var data = await client.GetStringAsync(hidden_data_url);

            List<HitomiArticle> articles = JsonConvert.DeserializeObject<List<HitomiArticle>>(data);
            JsonSerializer serializer = new JsonSerializer();
            serializer.Converters.Add(new JavaScriptDateTimeConverter());
            serializer.NullValueHandling = NullValueHandling.Ignore;

            if (!Settings.Instance.Hitomi.AutoSync)
            {
                Monitor.Instance.Push("Write file: hiddendata.json");
                using (StreamWriter sw = new StreamWriter(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "hiddendata.json")))
                using (JsonWriter writer = new JsonTextWriter(sw))
                {
                    serializer.Serialize(writer, articles);
                }
            }

            HashSet<string> overlap = new HashSet<string>();
            metadata_collection.ForEach(x => overlap.Add(x.ID.ToString()));
            foreach (var article in articles)
            {
                if (overlap.Contains(article.Magic)) continue;
                metadata_collection.Add(HitomiLegalize.ArticleToMetadata(article));
                if (!thumbnail_collection.ContainsKey(article.Magic))
                    thumbnail_collection.Add(article.Magic, article.Thumbnail);
            }
            SortMetadata();
            return;
        }

        public void SortMetadata()
        {
            metadata_collection.Sort((a, b) => b.ID.CompareTo(a.ID));
        }

        private async Task downloadMetadata(int no)
        {
            int retry = 0;
        RETRYL:
            if (retry > 10)
            {
                Monitor.Instance.Push("Maximum number(10) of retries has been exceeded.");
                Monitor.Instance.Push("Sorry");
                return;
            }
            try
            {
                HttpClient client = new HttpClient();
                client.Timeout = new TimeSpan(0, 0, 0, 0, Timeout.Infinite);
                var data = await client.GetStringAsync(gallerie_json_uri(no));
                if (data.Trim() == "")
                {
                    Monitor.Instance.Push($"Error: '{gallerie_json_uri(no)}' is empty database!");
                    return;
                }
                lock (metadata_collection)
                    metadata_collection.AddRange(JsonConvert.DeserializeObject<IEnumerable<HitomiMetadata>>(data));
                lock (lock_count)
                {
                    downloadCount++;
                    Monitor.Instance.Push($"Download complete: [{downloadCount.ToString("00")}/{number_of_gallery_jsons}] {gallerie_json_uri(no)}");
                }
            }
            catch (Exception e)
            {
                Monitor.Instance.Push($"Error: " + e.Message);
                Monitor.Instance.Push($"Retrying download ... [{no}|{retry}]");
                retry++;
                Thread.Sleep(1000);
                goto RETRYL;
            }
        }
        
        public void LoadMetadataJson()
        {
            if (CheckMetadataExist())
                metadata_collection = JsonConvert.DeserializeObject<List<HitomiMetadata>>(File.ReadAllText(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "metadata.json")));
        }

        public void LoadHiddendataJson()
        {
            thumbnail_collection = new Dictionary<string, string>();
            if (CheckHiddendataExist())
            {
                List<HitomiArticle> articles = JsonConvert.DeserializeObject<List<HitomiArticle>>(File.ReadAllText(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "hiddendata.json")));
                HashSet<string> overlap = new HashSet<string>();
                if (metadata_collection == null)
                    metadata_collection = new List<HitomiMetadata>();
                metadata_collection.ForEach(x => overlap.Add(x.ID.ToString()));
                foreach (var article in articles)
                {
                    if (overlap.Contains(article.Magic)) continue;
                    metadata_collection.Add(HitomiLegalize.ArticleToMetadata(article));
                    if (!thumbnail_collection.ContainsKey(article.Magic))
                        thumbnail_collection.Add(article.Magic, article.Thumbnail);
                }
                SortMetadata();
            }
        }

        public bool CheckMetadataExist()
        {
            return File.Exists(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "metadata.json"));
        }

        public bool CheckHiddendataExist()
        {
            return File.Exists(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "hiddendata.json"));
        }
        #endregion

        #region Metadata Testing
        public void LoadMetadataJson(string path)
        {
            metadata_collection.AddRange(JsonConvert.DeserializeObject<List<HitomiMetadata>>(File.ReadAllText(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), path))));
        }

        public DateTime GetLatestMetadataUpdateTime()
        {
            return File.GetLastWriteTime("metadata.json");
        }
        #endregion

        public void OptimizeMetadata()
        {
            List<HitomiMetadata> tmeta = new List<HitomiMetadata>();
            int m = metadata_collection.Count;
            for (int i = 0; i < m; i++)
            {
                string lang = metadata_collection[i].Language;
                if (metadata_collection[i].Language == null) lang = "N/A";
                if (Settings.Instance.Hitomi.Language != "ALL" &&
                    Settings.Instance.Hitomi.Language != lang)
                    continue;
                tmeta.Add(metadata_collection[i]);
            }
            metadata_collection.Clear();
            metadata_collection = tmeta;
        }

        #region TagData
        public void SortTagdata()
        {
            tagdata_collection.artist.Sort((a, b) => b.Count.CompareTo(a.Count));
            tagdata_collection.tag.Sort((a, b) => b.Count.CompareTo(a.Count));
            tagdata_collection.female.Sort((a, b) => b.Count.CompareTo(a.Count));
            tagdata_collection.male.Sort((a, b) => b.Count.CompareTo(a.Count));
            tagdata_collection.group.Sort((a, b) => b.Count.CompareTo(a.Count));
            tagdata_collection.character.Sort((a, b) => b.Count.CompareTo(a.Count));
            tagdata_collection.series.Sort((a, b) => b.Count.CompareTo(a.Count));
            tagdata_collection.type.Sort((a, b) => b.Count.CompareTo(a.Count));
        }
        #endregion

        #region TagData Rebuilding

        private void Add(Dictionary<string, int> dic, string key)
        {
            if (dic.ContainsKey(key))
                dic[key] += 1;
            else
                dic.Add(key, 1);
        }

        public void RebuildTagData()
        {
            tagdata_collection.artist?.Clear();
            tagdata_collection.tag?.Clear();
            tagdata_collection.female?.Clear();
            tagdata_collection.male?.Clear();
            tagdata_collection.group?.Clear();
            tagdata_collection.character?.Clear();
            tagdata_collection.series?.Clear();
            tagdata_collection.type?.Clear();

            HashSet<string> language = new HashSet<string>();

            foreach (var metadata in metadata_collection)
            {
                if (metadata.Language != null)
                {
                    string lang = metadata.Language.Trim();
                    if (lang != "" && !language.Contains(metadata.Language))
                        language.Add(metadata.Language);
                }
            }

            tagdata_collection.language = language.Select(x => new HitomiTagdata() { Tag = x }).ToList();
            tagdata_collection.language.Sort((a, b) => a.Tag.CompareTo(b.Tag));

            Dictionary<string, int> artist = new Dictionary<string, int>();
            Dictionary<string, int> tag = new Dictionary<string, int>();
            Dictionary<string, int> female = new Dictionary<string, int>();
            Dictionary<string, int> male = new Dictionary<string, int>();
            Dictionary<string, int> group = new Dictionary<string, int>();
            Dictionary<string, int> character = new Dictionary<string, int>();
            Dictionary<string, int> series = new Dictionary<string, int>();
            Dictionary<string, int> type = new Dictionary<string, int>();

            foreach (var metadata in metadata_collection)
            {
                string lang = metadata.Language;
                if (metadata.Language == null) lang = "N/A";
                if (Settings.Instance.Hitomi.Language != "ALL" &&
                    Settings.Instance.Hitomi.Language != lang) continue;
                if (metadata.Artists != null) metadata.Artists.ToList().ForEach(x => Add(artist, x));
                if (metadata.Tags != null) metadata.Tags.ToList().ForEach(x => { if (x.StartsWith("female:")) Add(female, x); else if (x.StartsWith("male:")) Add(male, x); else Add(tag, x); });
                if (metadata.Groups != null) metadata.Groups.ToList().ForEach(x => Add(group, x));
                if (metadata.Characters != null) metadata.Characters.ToList().ForEach(x => Add(character, x));
                if (metadata.Parodies != null) metadata.Parodies.ToList().ForEach(x => Add(series, x));
                if (metadata.Type != null) Add(type, metadata.Type);
            }

            tagdata_collection.artist = artist.Select(x => new HitomiTagdata() { Tag = x.Key, Count = x.Value }).ToList();
            tagdata_collection.tag = tag.Select(x => new HitomiTagdata() { Tag = x.Key, Count = x.Value }).ToList();
            tagdata_collection.female = female.Select(x => new HitomiTagdata() { Tag = x.Key, Count = x.Value }).ToList();
            tagdata_collection.male = male.Select(x => new HitomiTagdata() { Tag = x.Key, Count = x.Value }).ToList();
            tagdata_collection.group = group.Select(x => new HitomiTagdata() { Tag = x.Key, Count = x.Value }).ToList();
            tagdata_collection.character = character.Select(x => new HitomiTagdata() { Tag = x.Key, Count = x.Value }).ToList();
            tagdata_collection.series = series.Select(x => new HitomiTagdata() { Tag = x.Key, Count = x.Value }).ToList();
            tagdata_collection.type = type.Select(x => new HitomiTagdata() { Tag = x.Key, Count = x.Value }).ToList();

            SortTagdata();

            JsonSerializer serializer = new JsonSerializer();
            serializer.Converters.Add(new JavaScriptDateTimeConverter());
            serializer.NullValueHandling = NullValueHandling.Ignore;

            using (StreamWriter sw = new StreamWriter(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "tagdata.json")))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, tagdata_collection);
            }
        }
        #endregion

        public async Task Synchronization()
        {
            Monitor.Instance.Push("Start Synchronization...");
            metadata_collection?.Clear();
            thumbnail_collection?.Clear();
            await Task.Run(() => DownloadMetadata());
            await Task.Run(() => DownloadHiddendata());
            await Task.Run(() => RebuildTagData());
            await Task.Run(() => SortTagdata());
            if (Settings.Instance.Hitomi.UsingOptimization)
                await Task.Run(() => OptimizeMetadata());
            Monitor.Instance.Push("End Synchronization");
            Monitor.Instance.Push($"Sync Report : {metadata_collection.Count} {tagdata_collection.female.Count}");
        }
    }
}
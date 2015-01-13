﻿using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using NLog;
using NzbDrone.Common;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Tv;
using NzbDrone.Core.DataAugmentation.Sickbeard;

namespace NzbDrone.Core.RootFolders
{
    public interface IRootFolderService
    {
        List<RootFolder> All();
        List<RootFolder> AllWithUnmappedFolders();
        RootFolder Add(RootFolder rootDir);
        void Remove(int id);
        RootFolder Get(int id);
    }

    public class RootFolderService : IRootFolderService
    {
        private readonly ISickbeardService _sickbeardService;
        private readonly IRootFolderRepository _rootFolderRepository;
        private readonly IDiskProvider _diskProvider;
        private readonly ISeriesRepository _seriesRepository;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        private static readonly HashSet<string> SpecialFolders = new HashSet<string>
                                                                 {
                                                                     "$recycle.bin",
                                                                     "system volume information",
                                                                     "recycler",
                                                                     "lost+found",
                                                                     ".appledb",
                                                                     ".appledesktop",
                                                                     ".appledouble",
                                                                     "@eadir"
                                                                 };


        public RootFolderService(ISickbeardService sickbeardService,
                                 IRootFolderRepository rootFolderRepository,
                                 IDiskProvider diskProvider,
                                 ISeriesRepository seriesRepository,
                                 IConfigService configService,
                                 Logger logger)
        {
            _sickbeardService = sickbeardService;
            _rootFolderRepository = rootFolderRepository;
            _diskProvider = diskProvider;
            _seriesRepository = seriesRepository;
            _configService = configService;
            _logger = logger;
        }

        public List<RootFolder> All()
        {
            var rootFolders = _rootFolderRepository.All().ToList();

            return rootFolders;
        }

        public List<RootFolder> AllWithUnmappedFolders()
        {
            var rootFolders = _rootFolderRepository.All().ToList();

            rootFolders.ForEach(folder =>
            {
                if (folder.Path.IsPathValid() && _diskProvider.FolderExists(folder.Path))
                {
                    folder.FreeSpace = _diskProvider.GetAvailableSpace(folder.Path);
                    folder.UnmappedFolders = GetUnmappedFolders(folder.Path);
                }
            });

            return rootFolders;
        }

        public RootFolder Add(RootFolder rootFolder)
        {
            var all = All();

            if (String.IsNullOrWhiteSpace(rootFolder.Path) || !Path.IsPathRooted(rootFolder.Path))
                throw new ArgumentException("Invalid path");

            if (!_diskProvider.FolderExists(rootFolder.Path))
                throw new DirectoryNotFoundException("Can't add root directory that doesn't exist.");

            if (all.Exists(r => r.Path.PathEquals(rootFolder.Path)))
                throw new InvalidOperationException("Recent directory already exists.");

            if (!String.IsNullOrWhiteSpace(_configService.DownloadedEpisodesFolder) &&
                _configService.DownloadedEpisodesFolder.PathEquals(rootFolder.Path))
                throw new InvalidOperationException("Drone Factory folder cannot be used.");

            _rootFolderRepository.Insert(rootFolder);

            rootFolder.FreeSpace = _diskProvider.GetAvailableSpace(rootFolder.Path);
            rootFolder.UnmappedFolders = GetUnmappedFolders(rootFolder.Path);
            return rootFolder;
        }

        public void Remove(int id)
        {
            _rootFolderRepository.Delete(id);
        }

        private List<UnmappedFolder> GetUnmappedFolders(string path)
        {
            _logger.Debug("Generating list of unmapped folders");
            if (String.IsNullOrEmpty(path))
                throw new ArgumentException("Invalid path provided", "path");

            var results = new List<UnmappedFolder>();
            var series = _seriesRepository.All().ToList();

            if (!_diskProvider.FolderExists(path))
            {
                _logger.Debug("Path supplied does not exist: {0}", path);
                return results;
            }

            var seriesFolders = _diskProvider.GetDirectories(path).ToList();
            var unmappedFolders = seriesFolders.Except(series.Select(s => s.Path), PathEqualityComparer.Instance).ToList();

            foreach (string unmappedFolder in unmappedFolders)
            {
                var di = new DirectoryInfo(unmappedFolder.Normalize());
                results.Add(new UnmappedFolder { Name = di.Name, Path = di.FullName });
            }

            var setToRemove = SpecialFolders;
            results.RemoveAll(x => setToRemove.Contains(new DirectoryInfo(x.Path.ToLowerInvariant()).Name));
            
            if (_sickbeardService.DatabaseExists)
            {
                var allSbSeries = _sickbeardService.GetAllSeries().ToDictionary(v => v.Path, PathEqualityComparer.Instance);

                foreach (var unmappedFolder in results)
                {
                    Series sbSeries;
                    if (allSbSeries.TryGetValue(unmappedFolder.Path, out sbSeries))
                    {
                        unmappedFolder.Name = "tvdb:" + sbSeries.TvdbId.ToString();
                    }
                }

                return results;
            }

            _logger.Debug("{0} unmapped folders detected.", results.Count);
            return results;
        }

        public RootFolder Get(int id)
        {
            var rootFolder = _rootFolderRepository.Get(id);
            rootFolder.FreeSpace = _diskProvider.GetAvailableSpace(rootFolder.Path);
            rootFolder.UnmappedFolders = GetUnmappedFolders(rootFolder.Path);
            return rootFolder;
        }
    }
}
﻿using OurFoodChain.Common;
using OurFoodChain.Common.Utilities;
using OurFoodChain.Data;
using OurFoodChain.Debug;
using OurFoodChain.Trophies.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OurFoodChain.Trophies {

    public abstract class TrophyScannerBase :
         ITrophyScanner {

        // Public members

        public event EventHandler<LogMessage> Log;
        public event EventHandler<IUnlockedTrophyInfo> TrophyUnlocked;

        public int ScanDelay => 60 * 5; // 5 minutes

        public async Task EnqueueAsync(ICreator creator, bool scanImmediately = false) {

            // If the user already exists in the queue, don't add them again.

            if (creator.UserId.HasValue && !queue.Any(item => item.Creator.UserId == creator.UserId)) {

                // Add the user to the scanner queue.

                queue.Enqueue(new QueueItem {
                    Creator = creator,
                    DateAdded = scanImmediately ? DateTimeOffset.MinValue : DateUtilities.GetCurrentDate()
                });

                // Since there's something in the queue, start the trophy scanner (nothing will happen if it's already active).

                await StartScannerAsync();

            }

        }

        public IEnumerable<ITrophy> GetTrophies() {

            return trophies;

        }
        public void RegisterTrophies() {

            // Register all trophies in the assembly.

            foreach (Type type in AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(ITrophy)))
                .Where(type => type.GetConstructor(Type.EmptyTypes) != null)) {

                ITrophy instance = (ITrophy)Activator.CreateInstance(type);

                trophies.Add(instance);

            }

        }
        public void RegisterTrophy(ITrophy trophy) {

            trophies.Add(trophy);

        }

        // Protected members

        protected TrophyScannerBase(SQLiteDatabase database) {

            this.database = database;

        }

        protected void OnLog(LogMessage logMessage) {

            Log?.Invoke(this, logMessage);

        }
        protected void OnLog(LogSeverity severity, string message) {

            OnLog(new LogMessage(severity, "Trophies", message));

        }
        protected void OnTrophyUnlocked(IUnlockedTrophyInfo info) {

            TrophyUnlocked?.Invoke(this, info);

        }

        // Private members

        private class QueueItem {

            public ICreator Creator { get; set; }
            public DateTimeOffset DateAdded { get; set; }

        }

        private readonly SQLiteDatabase database;
        private readonly List<ITrophy> trophies = new List<ITrophy>();
        private readonly ConcurrentQueue<QueueItem> queue = new ConcurrentQueue<QueueItem>();
        private bool scannerIsActive = false;

        private async Task StartScannerAsync() {

            if (scannerIsActive)
                return;

            scannerIsActive = true;

            _ = Task.Run(async () => {

                OnLog(LogSeverity.Info, "Starting trophy scanner");

                while (queue.Count > 0) {

                    // Wait until the next item in the queue has been sitting for at least SCAN_DELAY.

                    DateTimeOffset currentDate = DateUtilities.GetCurrentDate();
                    DateTimeOffset itemDate = queue.First().DateAdded;

                    if ((currentDate - itemDate).TotalSeconds < ScanDelay)
                        await Task.Delay(TimeSpan.FromSeconds(ScanDelay - (currentDate - itemDate).TotalSeconds));

                    // Scan trophies for the next item in the queue.

                    if (queue.TryDequeue(out QueueItem item))
                        await ScanTrophiesAsync(item);

                }

                // When we've processed all users in the queue, shut down the scanner.

                scannerIsActive = false;

                OnLog(LogSeverity.Info, "Shutting down trophy scanner");

            });

            await Task.CompletedTask;

        }
        private async Task ScanTrophiesAsync(QueueItem item) {

            // Get the trophies the user has already unlocked so we don't pop trophies that have already been popped.

            IEnumerable<IUnlockedTrophyInfo> alreadyUnlocked = await database.GetUnlockedTrophiesAsync(item.Creator, GetTrophies());
            HashSet<string> alreadyUnlockedIdentifiers = new HashSet<string>();

            foreach (UnlockedTrophyInfo info in alreadyUnlocked)
                alreadyUnlockedIdentifiers.Add(info.Trophy.Identifier);

            // Check for new trophies that the user has just unlocked.

            ICheckTrophyContext context = new CheckTrophyContext(database, item.Creator);

            foreach (ITrophy trophy in GetTrophies())

                try {

                    if (!alreadyUnlockedIdentifiers.Contains(trophy.Identifier) && await trophy.CheckTrophyAsync(context)) {

                        // Insert new trophy into the database.

                        await database.UnlockTrophyAsync(item.Creator, trophy);

                        // Pop the new trophy.

                        OnTrophyUnlocked(new UnlockedTrophyInfo(item.Creator, trophy));

                    }

                }
                // If an error occurs when checking a trophy, we'll just move on to the next one.
                catch (Exception ex) {

                    OnLog(LogSeverity.Error, string.Format("Exception occured while checking \"{0}\" trophy: {1}",
                        trophy.Name,
                        ex.ToString()));

                }

        }

    }

}
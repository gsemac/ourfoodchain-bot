﻿using Discord.Commands;
using OurFoodChain.Adapters;
using OurFoodChain.Bot.Attributes;
using OurFoodChain.Common.Extensions;
using OurFoodChain.Common.Taxa;
using OurFoodChain.Common.Zones;
using OurFoodChain.Data;
using OurFoodChain.Data.Extensions;
using OurFoodChain.Data.Queries;
using OurFoodChain.Discord.Messaging;
using OurFoodChain.Discord.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OurFoodChain.Bot.Modules {

    public class BulkModule :
        OfcModuleBase {

        [Command("bulk"), RequirePrivilege(PrivilegeLevel.ServerModerator), DifficultyLevel(DifficultyLevel.Advanced)]
        public async Task Bulk([Remainder]string operationString) {

            // Instantiate the bulk operation and get the query results.

            Taxa.IBulkOperation bulkOperation = new Taxa.BulkOperation(operationString);
            ISearchResult queryResult = await Db.GetSearchResultsAsync(SearchContext, bulkOperation.Query);

            if (queryResult.Count() <= 0) {

                await ReplyInfoAsync("No species matching this query could be found.");

            }
            else {

                // Perform the requested operation.

                switch (bulkOperation.OperationName) {

                    case "addto": {

                            // Move the species into the given zone.

                            if (bulkOperation.Arguments.Count() != 1)
                                throw new Exception(string.Format("This operation requires {0} argument(s), but was given {1}.", 1, bulkOperation.Arguments.Count()));

                            string zoneName = bulkOperation.Arguments.First();
                            IZone zone = await Db.GetZoneAsync(zoneName);

                            if (await BotUtils.ReplyValidateZoneAsync(Context, zone, zoneName)) {

                                IPaginatedMessage message = new Discord.Messaging.PaginatedMessage(string.Format("**{0}** species will be added to **{1}**. Is this OK?", queryResult.Count(), zone.GetFullName())) {
                                    Restricted = true
                                };

                                message.AddReaction(PaginatedMessageReactionType.Yes, async (args) => {

                                    await ReplyInfoAsync("Performing the requested operation.");

                                    foreach (ISpecies species in await queryResult.GetResultsAsync()) {

                                        //await SpeciesUtils.RemoveZonesAsync(species, (await SpeciesUtils.GetZonesAsync(species)).Select(z => z.Zone));
                                        await Db.AddZonesAsync(species, new IZone[] { zone });

                                    }

                                    await ReplySuccessAsync("Operation completed successfully.");

                                });

                                await ReplyAsync(message);

                            }

                        }
                        break;

                    default:

                        await ReplyErrorAsync(string.Format("Unknown operation \"{0}\".", bulkOperation.OperationName));

                        break;

                }

            }

        }

    }

}
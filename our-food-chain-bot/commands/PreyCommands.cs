﻿using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OurFoodChain {
    public class PreyCommands :
        ModuleBase {


        [Command("+prey"), Alias("setprey", "seteats", "setpredates")]
        public async Task SetPredates(string speciesName, string preySpeciesName) {

            if (preySpeciesName.Any(x => x == ',' || x == '/' || x == '\\'))
                // The user has specified multiple prey items in a list.
                await _setPredates(string.Empty, speciesName, preySpeciesName.Split(',', '/', '\\'), string.Empty);
            else
                // The user has provided a single prey item.
                await SetPredates(string.Empty, speciesName, string.Empty, preySpeciesName);
        }
        [Command("+prey"), Alias("setprey", "seteats", "setpredates")]
        public async Task SetPredates(string arg0, string arg1, string arg2) {

            // We have the following possibilities, which we will check for in-order:
            // <genusName> <speciesName> <preySpeciesName>
            // <speciesName> <preyGenusName> <preySpeciesName>
            // <speciesName> <preySpecieName> <Notes>

            // If the user provided a prey list, it's easier to determine what they meant-- Check for that first.

            if (_isSpeciesList(arg1))
                await _setPredates(string.Empty, arg0, _splitSpeciesList(arg1), arg2);
            else if (_isSpeciesList(arg2))
                await _setPredates(arg0, arg1, _splitSpeciesList(arg2), string.Empty);
            else {

                Species species = null, preySpecies = null;
                string notes = string.Empty;

                // <genusName> <speciesName> <preySpeciesName>

                species = await SpeciesUtils.GetUniqueSpeciesAsync(arg0, arg1);
                preySpecies = species is null ? null : await SpeciesUtils.GetUniqueSpeciesAsync(arg2);
                notes = string.Empty;

                if (species is null || preySpecies is null) {

                    // <speciesName> <preyGenusName> <preySpeciesName>

                    species = await SpeciesUtils.GetUniqueSpeciesAsync(arg0);
                    preySpecies = species is null ? null : await SpeciesUtils.GetUniqueSpeciesAsync(arg1, arg2);
                    notes = string.Empty;

                }

                if (species is null || preySpecies is null) {

                    // <speciesName> <preySpecieName> <Notes>

                    species = await SpeciesUtils.GetUniqueSpeciesAsync(arg0);
                    preySpecies = species is null ? null : await SpeciesUtils.GetUniqueSpeciesAsync(arg1);
                    notes = arg2;

                }

                if (species is null)
                    await BotUtils.ReplyAsync_Error(Context, "The given species does not exist.");
                else if (preySpecies is null)
                    await BotUtils.ReplyAsync_Error(Context, "The given prey species does not exist.");
                else
                    await _setPredates(species, new Species[] { preySpecies }, notes);

            }

        }
        [Command("+prey"), Alias("setprey", "seteats", "setpredates")]
        public async Task SetPredates(string genusName, string speciesName, string preyGenusName, string preySpeciesName, string notes = "") {

            Species[] species_list = await SpeciesUtils.GetSpeciesAsync(genusName, speciesName);
            Species[] prey_list = await SpeciesUtils.GetSpeciesAsync(preyGenusName, preySpeciesName);

            if (species_list.Count() <= 0)
                await BotUtils.ReplyAsync_SpeciesSuggestions(Context, genusName, speciesName);
            else if (prey_list.Count() <= 0)
                await BotUtils.ReplyAsync_SpeciesSuggestions(Context, preyGenusName, preySpeciesName);
            else if (!await BotUtils.ReplyAsync_ValidateSpecies(Context, species_list) || !await BotUtils.ReplyAsync_ValidateSpecies(Context, prey_list))
                return;
            else
                await _setPredates(species_list[0], prey_list, notes);

        }

        private bool _isSpeciesList(string input) {
            return _splitSpeciesList(input).Count() > 1;
        }
        private string[] _splitSpeciesList(string input) {
            return input.Split(',', '/', '\\');
        }
        private async Task _setPredates(string genusName, string speciesName, string[] preySpeciesNames, string notes) {

            Species[] species_list = await SpeciesUtils.GetSpeciesAsync(genusName, speciesName);

            if (species_list.Count() <= 0)
                await BotUtils.ReplyAsync_SpeciesSuggestions(Context, genusName, speciesName);
            else {

                List<Species> prey_list = new List<Species>();
                List<string> failed_prey = new List<string>();

                foreach (string prey_species_name in preySpeciesNames) {

                    Species prey = await SpeciesUtils.GetUniqueSpeciesAsync(prey_species_name);

                    if (prey is null)
                        failed_prey.Add(prey_species_name);
                    else
                        prey_list.Add(prey);

                }

                if (failed_prey.Count() > 0)
                    await BotUtils.ReplyAsync_Warning(Context, string.Format("The following species could not be determined: {0}.",
                       StringUtils.ConjunctiveJoin(", ", failed_prey.Select(x => string.Format("**{0}**", StringUtils.ToTitleCase(x))).ToArray())));

                if (prey_list.Count() > 0)
                    await _setPredates(species_list[0], prey_list.ToArray(), notes);

            }

        }
        private async Task _setPredates(Species species, Species[] preySpecies, string notes) {

            // Ensure that the user has necessary privileges.
            if (!await BotUtils.ReplyHasPrivilegeOrOwnershipAsync(Context, PrivilegeLevel.ServerModerator, species))
                return;

            foreach (Species prey in preySpecies) {

                using (SQLiteCommand cmd = new SQLiteCommand("INSERT OR REPLACE INTO Predates(species_id, eats_id, notes) VALUES($species_id, $eats_id, $notes)")) {

                    cmd.Parameters.AddWithValue("$species_id", species.id);
                    cmd.Parameters.AddWithValue("$eats_id", prey.id);
                    cmd.Parameters.AddWithValue("$notes", notes);

                    await Database.ExecuteNonQuery(cmd);

                }

            }

            await BotUtils.ReplyAsync_Success(Context, string.Format("**{0}** now preys upon {1}.",
                species.GetShortName(),
                StringUtils.ConjunctiveJoin(", ", preySpecies.Select(x => string.Format("**{0}**", x.GetShortName())).ToArray())
                ));

        }

        [Command("-prey")]
        public async Task RemovePrey(string species, string eatsSpecies) {
            await RemovePrey("", species, "", eatsSpecies);
        }
        [Command("-prey")]
        public async Task RemovePrey(string genus, string species, string eatsGenus, string eatsSpecies) {

            // Get the predator and prey species.

            Species predator = await BotUtils.ReplyAsync_FindSpecies(Context, genus, species);
            Species prey = await BotUtils.ReplyAsync_FindSpecies(Context, eatsGenus, eatsSpecies);

            if (predator is null || prey is null)
                return;

            // Remove the relationship.

            // Ensure that the user has necessary privileges to use this command.
            if (!await BotUtils.ReplyHasPrivilegeOrOwnershipAsync(Context, PrivilegeLevel.ServerModerator, predator))
                return;

            using (SQLiteCommand cmd = new SQLiteCommand("DELETE FROM Predates WHERE species_id=$species_id AND eats_id=$eats_id;")) {

                cmd.Parameters.AddWithValue("$species_id", predator.id);
                cmd.Parameters.AddWithValue("$eats_id", prey.id);

                await Database.ExecuteNonQuery(cmd);

            }

            await BotUtils.ReplyAsync_Success(Context, string.Format("**{0}** no longer preys upon **{1}**.", predator.GetShortName(), prey.GetShortName()));

        }

        [Command("predates"), Alias("eats", "pred", "predators")]
        public async Task Predates(string genus, string species = "") {

            // If the species parameter was not provided, assume the user only provided the species.
            if (string.IsNullOrEmpty(species)) {
                species = genus;
                genus = string.Empty;
            }

            Species sp = await BotUtils.ReplyAsync_FindSpecies(Context, genus, species);

            if (sp is null)
                return;

            EmbedBuilder embed = new EmbedBuilder();

            using (SQLiteCommand cmd = new SQLiteCommand("SELECT * FROM Predates WHERE eats_id=$eats_id AND species_id NOT IN (SELECT species_id FROM Extinctions);")) {

                cmd.Parameters.AddWithValue("$eats_id", sp.id);

                using (DataTable rows = await Database.GetRowsAsync(cmd)) {

                    if (rows.Rows.Count <= 0)
                        await BotUtils.ReplyAsync_Info(Context, string.Format("**{0}** has no extant natural predators.", sp.GetShortName()));
                    else {

                        List<string> lines = new List<string>();

                        foreach (DataRow row in rows.Rows) {

                            Species s = await BotUtils.GetSpeciesFromDb(row.Field<long>("species_id"));
                            string notes = row.Field<string>("notes");

                            string line_text = s.GetShortName();

                            if (!string.IsNullOrEmpty(notes))
                                line_text += string.Format(" ({0})", notes.ToLower());

                            lines.Add(s.isExtinct ? string.Format("~~{0}~~", line_text) : line_text);

                        }

                        lines.Sort();

                        embed.WithTitle(string.Format("Predators of {0} ({1})", sp.GetShortName(), lines.Count()));
                        embed.WithDescription(string.Join(Environment.NewLine, lines));

                        await ReplyAsync("", false, embed.Build());

                    }

                }

            }

        }

        [Command("prey")]
        public async Task Prey(string genus, string species = "") {

            // If no species argument was provided, assume the user omitted the genus.
            if (string.IsNullOrEmpty(species)) {
                species = genus;
                genus = string.Empty;
            }

            // Get the specified species.

            Species sp = await BotUtils.ReplyAsync_FindSpecies(Context, genus, species);

            if (sp is null)
                return;

            // Get the preyed-upon species.

            using (SQLiteCommand cmd = new SQLiteCommand("SELECT * FROM Predates WHERE species_id=$species_id;")) {

                cmd.Parameters.AddWithValue("$species_id", sp.id);

                using (DataTable rows = await Database.GetRowsAsync(cmd)) {

                    if (rows.Rows.Count <= 0)
                        await BotUtils.ReplyAsync_Info(Context, string.Format("**{0}** does not prey upon any other species.", sp.GetShortName()));
                    else {

                        List<Tuple<Species, string>> prey_list = new List<Tuple<Species, string>>();

                        foreach (DataRow row in rows.Rows) {

                            prey_list.Add(new Tuple<Species, string>(
                                await BotUtils.GetSpeciesFromDb(row.Field<long>("eats_id")),
                                row.Field<string>("notes")));

                        }

                        prey_list.Sort((lhs, rhs) => lhs.Item1.GetShortName().CompareTo(rhs.Item1.GetShortName()));

                        StringBuilder description = new StringBuilder();

                        foreach (Tuple<Species, string> prey in prey_list) {

                            description.Append(prey.Item1.isExtinct ? BotUtils.Strikeout(prey.Item1.GetShortName()) : prey.Item1.GetShortName());

                            if (!string.IsNullOrEmpty(prey.Item2))
                                description.Append(string.Format(" ({0})", prey.Item2.ToLower()));

                            description.AppendLine();

                        }

                        EmbedBuilder embed = new EmbedBuilder();

                        embed.WithTitle(string.Format("Species preyed upon by {0} ({1})", sp.GetShortName(), prey_list.Count()));
                        embed.WithDescription(description.ToString());

                        await ReplyAsync("", false, embed.Build());

                    }

                }

            }

        }

    }
}
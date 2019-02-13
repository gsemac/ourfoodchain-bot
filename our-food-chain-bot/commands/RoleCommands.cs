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

    public class RoleCommands :
        ModuleBase {

        [Command("addrole")]
        public async Task AddRole(string name, string description = "") {

            // Ensure that the user has necessary privileges to use this command.
            if (!await BotUtils.ReplyAsync_CheckPrivilege(Context, (IGuildUser)Context.User, PrivilegeLevel.ServerModerator))
                return;

            Role role = new Role {
                name = name,
                description = description
            };

            await BotUtils.AddRoleToDb(role);

            await BotUtils.ReplyAsync_Success(Context, string.Format("Succesfully created the new role **{0}**.", name));

        }

        [Command("+role"), Alias("setrole")]
        public async Task SetRole(string species, string role) {
            await SetRole("", species, role, "");
        }
        [Command("+role"), Alias("setrole")]
        public async Task SetRole(string genus, string species, string role, string notes = "") {

            // Get the species.

            Species sp = await BotUtils.ReplyAsync_FindSpecies(Context, genus, species);

            if (sp is null)
                return;

            // Get the role.

            Role role_info = await BotUtils.GetRoleFromDb(role);

            if (!await BotUtils.ReplyAsync_ValidateRole(Context, role_info))
                return;

            // Update the species.

            using (SQLiteCommand cmd = new SQLiteCommand("INSERT OR REPLACE INTO SpeciesRoles(species_id, role_id, notes) VALUES($species_id, $role_id, $notes);")) {

                cmd.Parameters.AddWithValue("$species_id", sp.id);
                cmd.Parameters.AddWithValue("$role_id", role_info.id);
                cmd.Parameters.AddWithValue("$notes", notes);

                await Database.ExecuteNonQuery(cmd);

                await BotUtils.ReplyAsync_Success(Context, string.Format("**{0}** has successfully been assigned the role of **{1}**.", sp.GetShortName(), StringUtils.ToTitleCase(role_info.name)));

            }

        }

        [Command("-role"), Alias("unsetrole")]
        public async Task RemoveRole(string species, string role) {
            await RemoveRole("", species, role);
        }
        [Command("-role"), Alias("unsetrole")]
        public async Task RemoveRole(string genus, string species, string role) {

            // Get the species.

            Species sp = await BotUtils.ReplyAsync_FindSpecies(Context, genus, species);

            if (sp is null)
                return;

            // Get the role.

            Role role_info = await BotUtils.GetRoleFromDb(role);

            if (!await BotUtils.ReplyAsync_ValidateRole(Context, role_info))
                return;

            // Update the species.

            using (SQLiteCommand cmd = new SQLiteCommand("DELETE FROM SpeciesRoles WHERE species_id=$species_id AND role_id=$role_id;")) {

                cmd.Parameters.AddWithValue("$species_id", sp.id);
                cmd.Parameters.AddWithValue("$role_id", role_info.id);

                await Database.ExecuteNonQuery(cmd);

                await BotUtils.ReplyAsync_Success(Context, string.Format("Role **{0}** has successfully been unassigned from **{1}**.", StringUtils.ToTitleCase(role_info.name), sp.GetShortName()));

            }

        }

        [Command("roles"), Alias("role")]
        public async Task Roles() {

            EmbedBuilder embed = new EmbedBuilder();

            Role[] roles_list = await BotUtils.GetRolesFromDb();

            embed.WithTitle(string.Format("All roles ({0})", roles_list.Count()));

            foreach (Role role in roles_list) {

                long count = 0;

                using (SQLiteCommand cmd = new SQLiteCommand("SELECT count(*) FROM SpeciesRoles WHERE role_id=$role_id;")) {

                    cmd.Parameters.AddWithValue("$role_id", role.id);

                    count = (await Database.GetRowAsync(cmd)).Field<long>("count(*)");

                }

                string title = string.Format("{0} ({1})",
                    StringUtils.ToTitleCase(role.name),
                    count);

                embed.AddField(title, role.GetDescriptionOrDefault());

            }

            await ReplyAsync("", false, embed.Build());

        }
        [Command("roles"), Alias("role")]
        public async Task Roles(string nameOrSpecies) {

            // If a role with this name exists, that's what we'll prioritize (users can use the genus + species overload if they need to).
            // If no such role exists, check for a species with this name instead.

            Role role = await BotUtils.GetRoleFromDb(nameOrSpecies);

            if (role is null) {

                // No such role exists, so check if a species exists with the given name instead.

                Species[] matching_species = await BotUtils.GetSpeciesFromDb("", nameOrSpecies);

                if (matching_species.Count() == 1)
                    // If only one species was returned, show the roles assigned to that species.
                    await Roles(matching_species[0]);
                else if (matching_species.Count() > 1)
                    // If multiple species were returned, provide a list of matching species for the user to choose from.
                    await BotUtils.ReplyAsync_ValidateSpecies(Context, matching_species);

                if (matching_species.Count() > 0)
                    return;

            }

            // If we got here, the role is eiher not null, or it is null, but no species with the given name exists.
            // In this case, proceed to validate the role, and display its information if possible.

            if (!await BotUtils.ReplyAsync_ValidateRole(Context, role))
                return;

            EmbedBuilder embed = new EmbedBuilder();
            embed.WithTitle(string.Format("Role: {0}", StringUtils.ToTitleCase(role.name)));
            embed.WithDescription(role.GetDescriptionOrDefault());

            // List all extant species with this role.

            List<Species> species_list = new List<Species>(await BotUtils.GetSpeciesFromDbByRole(role));

            species_list.RemoveAll(x => x.isExtinct);
            species_list.Sort((lhs, rhs) => lhs.GetShortName().CompareTo(rhs.GetShortName()));

            if (species_list.Count() > 0) {

                StringBuilder lines = new StringBuilder();

                foreach (Species sp in species_list)
                    lines.AppendLine(sp.GetShortName());

                embed.WithDescription(string.Format("{2}\n\n**Extant species with this role ({1}):**\n{0}", lines.ToString(), species_list.Count(), role.GetDescriptionOrDefault()));

            }

            await ReplyAsync("", false, embed.Build());


        }
        [Command("roles"), Alias("role")]
        public async Task Roles(string genus, string species) {

            Species sp = await BotUtils.ReplyAsync_FindSpecies(Context, genus, species);

            if (!(sp is null))
                await Roles(sp);

        }
        public async Task Roles(Species species) {

            // Get the role(s) assigned to this species.

            Role[] roles = await BotUtils.GetRolesFromDbBySpecies(species);

            if (roles.Count() <= 0) {
                await BotUtils.ReplyAsync_Info(Context, string.Format("**{0}** has not been assigned any roles.", species.GetShortName()));
                return;
            }

            // Display the role(s) to the user.

            StringBuilder lines = new StringBuilder();

            foreach (Role i in roles) {

                lines.Append(StringUtils.ToTitleCase(i.name));

                if (!string.IsNullOrEmpty(i.notes))
                    lines.Append(string.Format(" ({0})", i.notes));

                lines.AppendLine();

            }

            EmbedBuilder embed = new EmbedBuilder();

            embed.WithTitle(string.Format("{0}'s role(s) ({1})", species.GetShortName(), roles.Count()));
            embed.WithDescription(lines.ToString());

            await ReplyAsync("", false, embed.Build());

        }

        [Command("setroledescription"), Alias("setroledesc")]
        public async Task SetRoleDescription(string name, string description) {

            // Ensure that the user has necessary privileges to use this command.
            if (!await BotUtils.ReplyAsync_CheckPrivilege(Context, (IGuildUser)Context.User, PrivilegeLevel.ServerModerator))
                return;

            Role role = await BotUtils.GetRoleFromDb(name);

            if (!await BotUtils.ReplyAsync_ValidateRole(Context, role))
                return;

            using (SQLiteCommand cmd = new SQLiteCommand("UPDATE Roles SET description=$description WHERE name=$name;")) {

                cmd.Parameters.AddWithValue("$name", role.name);
                cmd.Parameters.AddWithValue("$description", description);

                await Database.ExecuteNonQuery(cmd);

            }

            await BotUtils.ReplyAsync_Success(Context, string.Format("Successfully updated description for role **{0}**.", StringUtils.ToTitleCase(role.name)));

        }

    }

}
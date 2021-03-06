﻿using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using qtbot.CommandPlugin;
using System.Net.Http;
using System.Text.RegularExpressions;
using Discord;
using qtbot.BotTools;
using Discord.WebSocket;
using qtbot.CommandPlugin.Attributes;

namespace qtbot
{
    class AdminCommands
    {

        [Command("delete", CommandType.Admin, "d", "remove"), 
            Permission(Permission.ADMIN),
            Description("Delete messages on this channel. Usage: `-delete {number of messages to delete}`")]
        public static async Task DeleteMessage(CommandArgs e)
        {
            if ((e.Channel as IPrivateChannel) != null) //get right outta there if it's a private channel.
                return;

            bool silent = e.Args[e.Args.Count() - 1] == "-s";

            if (silent)
                await e.Message.DeleteAsync();

            //delete num
            int deleteNumber = 0;
            if (Int32.TryParse(e.Args[0], out deleteNumber))
            {

                if (deleteNumber + 1 >= 100)
                    deleteNumber = 99;

                var messages = await e.Channel.GetMessagesAsync(deleteNumber + 1).Flatten();

                foreach (var message in messages)
                {
                    await message.DeleteAsync();
                }

                if (!silent)
                    await BotTools.Tools.ReplyAsync(e, $"deleted {deleteNumber} messages!");
            }
            //Delete users' messages.
            else if (e.Message.MentionedUsers.Count() != 0)
            {
                var messages = await e.Channel.GetMessagesAsync().Flatten();

                var potentials = new List<IMessage>();

                foreach (var msg in messages)
                {
                    if (e.Message.MentionedUsers.Contains(msg.Author))
                        potentials.Add(msg);
                }

                if (potentials.Count() == 0)
                    return;

                foreach (var msg in potentials)
                {
                    await msg.DeleteAsync();
                }

                deleteNumber = potentials.Count();

                string users = "";
                for (int i = 0; i < e.Message.MentionedUsers.Count(); i++)
                {
                    if (i != 0)
                        users += ", ";
                    users += e.Message.MentionedUsers.ToArray()[i].Username;
                }

                if (!silent)
                    await BotTools.Tools.ReplyAsync(e, $"deleted {deleteNumber} messages by {users}!");
            }
            //embedded stuff
            else if (e.ArgText.StartsWith("embed"))
            {
                var messages = await e.Channel.GetMessagesAsync().Flatten();

                var potentials = new List<IMessage>();

                foreach (var msg in messages)
                {
                    if (msg.Embeds.Count() != 0)
                        potentials.Add(msg);
                }

                foreach (var msg in potentials)
                {
                    await msg.DeleteAsync();
                }

                deleteNumber = potentials.Count();

                if (!silent)
                    await Tools.ReplyAsync(e, $"deleted {deleteNumber} messages with embedded content!");
            }
            else if (e.ArgText.StartsWith("img"))
            {
                var messages = await e.Channel.GetMessagesAsync().Flatten();

                var potentials = new List<IMessage>();

                foreach (var msg in messages)
                {
                    if (msg.Attachments.Count() != 0)
                        potentials.Add(msg);
                }

                foreach (var msg in potentials)
                {
                    await msg.DeleteAsync();
                }

                deleteNumber = potentials.Count();

                if (!silent)
                    await Tools.ReplyAsync(e, $"deleted {deleteNumber} images!");
            }
            else
            {
                try
                {
                    Regex regex = new Regex("\"[^\"]*\"");
                    string text = regex.Match(e.ArgText).Value.TrimStart('"').TrimEnd('"');

                    if (text == "")
                        return;

                    var messages = await e.Channel.GetMessagesAsync().Flatten();

                    var potentials = new List<IMessage>();

                    foreach (var msg in messages)
                    {
                        if (msg.Content.ToLower().Contains(text.ToLower()))
                            potentials.Add(msg);
                    }

                    foreach (var msg in potentials)
                    {
                        await msg.DeleteAsync();
                    }


                    deleteNumber = potentials.Count();

                    if (!silent)
                        await Tools.ReplyAsync(e, $"deleted {deleteNumber} messages containing `{text}`!");
                }
                catch (Exception) { }
            }
        }

        [Command("permission add", CommandType.Admin, "pa"), 
            Permission(Permission.OWNER),
            Description("Assign a permission to a role. You can use: USER, ADMIN, or OWNER")]
        public static async Task AddPermissionToRank(CommandArgs e)
        {
            var serv = Tools.GetServerInfo(e.Guild.Id);

            string RoleToFind = "";
            for (int i = 0; i < e.Args.Length - 1; i++)
            {
                RoleToFind += e.Args[i] + " ";
            }
            RoleToFind = RoleToFind.Remove(RoleToFind.Length - 1);
            ulong roleId;

            try
            {
                roleId = e.Guild.Roles.FirstOrDefault(x => x.Name.Contains(RoleToFind)).Id;
                int permId;
                Permission perm;
                if(Enum.TryParse(e.Args[e.Args.Length - 1].ToUpper(), out perm))
                {
                    permId = (int)perm;
                }
                else
                {
                    await Tools.ReplyAsync(e, $"Unexpected permission `{e.Args[e.Args.Length - 1]}`. Possible values are: USER, ADMIN, or OWNER.");
                    return;
                }

                if (serv.roleImportancy.ContainsKey(roleId))
                {
                    serv.roleImportancy[roleId] = permId;
                }
                else
                {
                    serv.roleImportancy.Add(roleId, permId);
                }

                Tools.SaveServerInfo();

                await Tools.ReplyAsync(e, $"Role {e.Guild.GetRole(roleId).Name}'s permission setting has been set to {permId}.");
            }
            catch (Exception ex)
            {
                await Tools.ReplyAsync(e, ex.Message);
            }
        }


        [Command("timeout", CommandType.Admin, "t"),
            Description("toggle a time out, or time out a user for the defined time! Usage: `-t [user] [time (optional)]`"),
            Permission(Permission.ADMIN),
            Args(ArgsType.ArgsAtLeast)]
        public static async Task CmdTimeout(CommandArgs e)
        {
            if (e.Message.MentionedUsers.Count == 0)
                return;

            double timeoutTimeMinute = 0;
            if(double.TryParse(e.Args[e.Args.Length-1], System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.CurrentCulture, out timeoutTimeMinute))
            {
                if (timeoutTimeMinute <= 0)
                    await RemoveTimeout(e);
                else
                    await AddTimeout(e, timeoutTimeMinute);
            }
            else
            {
                var usr = e.Message.MentionedUsers.First() as IGuildUser;
                var role = e.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == "qttimedout");

                if (usr == null || role == null)
                    return;

                if (usr.RoleIds.Any(x => x == role.Id))
                    await RemoveTimeout(e);
                else
                    await AddTimeout(e);
            }
        }

        private static async Task RemoveTimeout(CommandArgs e, bool mention = true)
        {
            var usr = e.Message.MentionedUsers.First() as IGuildUser;

            var role = e.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == "qttimedout");

            if (usr == null || role == null ||
                !usr.RoleIds.Any(x => x == role.Id))
                return;

            var roles = usr.RoleIds.ToList();
            roles.Remove(role.Id);
            await usr.ModifyAsync(x => x.RoleIds = roles.ToArray());

            if(mention)
                await e.ReplyAsync($"Removed {usr.Mention}'s timeout.");
        }

        private static async Task AddTimeout(CommandArgs e, double time = -1)
        {
            var usr = e.Message.MentionedUsers.First() as IGuildUser;

            var role = e.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == "qttimedout");

            if (usr == null || role == null ||
                usr.RoleIds.Any(x => x == role.Id))
                return;

            var roles = usr.RoleIds.ToList();
            roles.Add(role.Id);
            await usr.ModifyAsync(x => x.RoleIds = roles.ToArray());

            if(time > 0)
            {
                await e.ReplyAsync($"Timed out {usr.Mention} for {time} minute(s).");
                await Task.Delay(TimeSpan.FromMinutes(time));
                await RemoveTimeout(e, false);
            }
            else
            {
                await e.ReplyAsync($"Timed out {usr.Mention} indefinitely.");
            }
            
        }

        [Command("permission remove", CommandType.Admin, "pr"),
            Permission(Permission.OWNER),
            Description("Remove the assigned permission from a role.")]
        public static async Task RemovePermission(CommandArgs e)
        {
            var userpermission = Tools.GetPerms(e, e.Author as IGuildUser);

            try
            {
                //Parse role
                string roleName = "";
                for (int i = 0; i < e.Args.Length; i++)
                {
                    roleName += e.Args[i] + " ";
                }
                //remove the space
                roleName = roleName.Remove(roleName.Length - 1);

                //Find the role
                ulong roleId;
                roleId = e.Guild.Roles.FirstOrDefault(x => x.Name.ToLower().Contains(roleName.ToLower())).Id;


                var a = Tools.GetServerInfo(e.Guild.Id);
                a.roleImportancy.Remove(roleId);
                Tools.SaveServerInfo();
            }
            catch (Exception ex)
            {
                await Tools.ReplyAsync(e, ex.Message);
            }
        }

        [Command("server edit", CommandType.Admin, "se"),
            Permission(Permission.OWNER),
            Description("Edit the server. Type `-server edit help` for " +
            "all available commands on this command.")]
        public static async Task EditServer(CommandArgs e)
        {
            //If there's only 1 word and not anymore, return.
            if (e.Args.Length < 1)
                return;

            ServerInfo info = Tools.GetServerInfo(e.Guild.Id);
            string toEdit = e.Args[0];
            string args = "";

            for (int i = 0; i < e.Args.Length; i++)
            {
                if (i == 0)
                    continue;

                args += e.Args[i] + " ";
            }

            if (args != "")
                args = args.Remove(args.Length - 1);

            switch (toEdit)
            {
                case "standardrole":
                    var role = e.Guild.Roles.FirstOrDefault(x => x.Name.ToLower().Contains(args.ToLower()));
                    info.standardRole = role.Id;
                    await Tools.ReplyAsync(e, $"{role.Name} is now the role that new users will automatically be upon joining the server!");
                    break;
                case "welcomechannel":
                    var channel = e.Message.MentionedChannels.FirstOrDefault();
                    info.welcomingChannel = channel.Id;
                    await Tools.ReplyAsync(e, $"{channel.Name} is now the channel that people will be welcomed to upon joining the server!");
                    break;
                case "safesearch":

                    if (args == "")
                    {
                        await Tools.ReplyAsync(e, "Possible options are `high, medium, off`. Otherwise it will fuck.");
                        return;
                    }

                    info.safesearch = args;
                    break;
                case "regular":
                    await Tools.ReplyAsync(e, "`regularrole` for roles.\n`regulerenabled` (false, true) to enable/disable.\n`regularamount` for amount." +
                        "\n'regulartime' to set the time.");
                    break;
                case "regularenabled":

                    if (args == "true")
                    {
                        info.RegularUsersEnabled = true;
                        await Tools.ReplyAsync(e, "Experience subsystem enabled.");
                    }
                    else
                    {
                        info.RegularUsersEnabled = false;
                        await Tools.ReplyAsync(e, "Experience subsystem disabled.");
                    }
                    break;
            }

            Tools.SaveServerInfo();
        }

        [Command("commands", CommandType.Admin),
            Permission(Permission.ADMIN), Hidden()]
        public static async Task GetAdminCommands(CommandArgs e)
        {
            string response = $"The character to use a command right now is '-'.\n";
            foreach (var cmd in Bot._commands.Commands)
            {
                if (cmd.commandType == CommandType.User)
                    continue;

                if (!String.IsNullOrWhiteSpace(cmd.Purpose))
                {
                    string command = "";
                    foreach (var cmdPart in cmd.Parts)
                        command += cmdPart + ' ';

                    response += $"**{command}** - {cmd.Purpose}";

                    if (cmd.CommandDelay == null)
                        response += "\n";
                    else
                        response += $" **|** Time limit: once per {cmd.CommandDelayNotify} {cmd.timeType}.\n";
                }
            }

            var c = await e.Author.CreateDMChannelAsync();
            await c.SendMessageAsync(response);
        }


        //Role management
        [Command("role", CommandType.Admin, "r"), 
            Permission(Permission.ADMIN),
            Description("Remove or add a role. Usage: `-role add/remove @{user(s)} Role name`")]
        public static async Task ManageUserRole(CommandArgs e)
        {
            var Args = e.Message.Content.Split(' ');
            Args = Args.Skip(1).ToArray();

            if (Args.Length < 3 || e.Message.MentionedUsers.Count() == 0 ||
            (e.Message.MentionedUsers.Any(x => x.Id == e.Author.Id) && Storage.programInfo.DevID.ToString() != e.Author.Id.ToString()))
            {
                await RoleSuccessFailAsync(false, e);
                return;
            }

            string roleName = "";
            for (int i = 1; i < Args.Length; i++) //i starts with 1 to avoid the "remove" or "add" arg
            {
                //If this parameter starts with a user mention, skip this.
                if (Args[i].StartsWith("<") && Args[i].EndsWith(">"))
                    continue;

                //If it's not a user mention, add it to the roleName.
                roleName += Args[i] + " ";
            }

            roleName = roleName.Trim();

            //Find the role
            SocketRole roleToGive;
            roleToGive = e.Guild.Roles.FirstOrDefault(x => x.Name.ToLower().Contains(roleName.ToLower()));

            if (roleToGive == null ||
            roleToGive.Position >= GetHighestRolePosition((e.Author as IGuildUser).RoleIds.ToArray(), e.Guild))
            {
                await RoleSuccessFailAsync(false, e);
                return;
            }

            List<IGuildUser> failedUsers = new List<IGuildUser>();

            //Give or add role
            switch (Args[0].ToLower())
            {
                case "add":
                case "a":

                    foreach (var user in e.Message.MentionedUsers)
                    {
                        var AuthorPosition = GetHighestRolePosition((e.Author as IGuildUser).RoleIds.ToArray(), e.Guild);
                        var EditedPosition = GetHighestRolePosition((user as IGuildUser).RoleIds.ToArray(), e.Guild);

                        var userRoles = (user as IGuildUser).RoleIds.ToList();
                        if (userRoles.Any(x => x == roleToGive.Id) ||
                        AuthorPosition <= EditedPosition)
                        {
                            failedUsers.Add(user as IGuildUser);
                            continue;
                        }


                        userRoles.Add(roleToGive.Id);
                        try { await (user as IGuildUser).ModifyAsync(z => z.RoleIds = userRoles); }
                        catch (Exception)
                        {
                            Console.WriteLine($"Couldn't edit {user.Username}");
                            failedUsers.Add(user as IGuildUser);
                        }
                    }

                    await RoleSuccessFailAsync(true, e, failedUsers.ToArray());

                    break;
                case "remove":
                case "r":

                    foreach (var user in e.Message.MentionedUsers)
                    {
                        var AuthorPosition = GetHighestRolePosition((e.Author as IGuildUser).RoleIds.ToArray(), e.Guild);
                        var EditedPosition = GetHighestRolePosition((user as IGuildUser).RoleIds.ToArray(), e.Guild);

                        var userRoles = (user as IGuildUser).RoleIds.ToList();
                        if (!userRoles.Any(x => x == roleToGive.Id) ||
                        AuthorPosition <= EditedPosition)
                        {
                            failedUsers.Add(user as IGuildUser);
                            continue;
                        }

                        userRoles.RemoveAll(x => x == roleToGive.Id);
                        try { await (user as IGuildUser).ModifyAsync(x => x.RoleIds = userRoles); }
                        catch (Exception)
                        {
                            Console.WriteLine($"Couldn't edit {user.Username}");
                            failedUsers.Add(user as IGuildUser);
                        }
                    }

                    await RoleSuccessFailAsync(true, e, failedUsers.ToArray());

                    break;
                default:
                    return;
            }
        }

        public static int GetHighestRolePosition(ulong[] ids, IGuild guild)
        {
            int position = -1;
            foreach (var id in ids)
            {
                var role = guild.GetRole(id);
                if (role.Position > position)
                    position = role.Position;
            }
            return position;
        }

        public static async Task RoleSuccessFailAsync(bool success, CommandArgs e, IGuildUser[] failedUsers = null)
        {
            var message = "";

            if (failedUsers != null && failedUsers.Length > 0)
            {
                message += ", ";
                foreach (var user in failedUsers)
                {
                    message += "But I failed to edit " + user.Mention + ".\n";
                }
            }

            if (success)
                await Tools.ReplyAsync(e, "👌🏽" + message, false);
            else
                await Tools.ReplyAsync(e, "🖕🏽" + message, false);
        }

        [Command("botnick", CommandType.Admin), 
            Permission(Permission.OWNER),
            Description("Change the bot's username. (Owner only)")]
        public static async Task ChangeBotNickname(CommandArgs e)
        {
            await (e.Guild.CurrentUser as IGuildUser).ModifyAsync(x => x.Nickname = e.ArgText);
        }

        [Command("botusername", CommandType.Admin), 
            Permission(Permission.BOTOWNER),
            Description("Change the bot's username. (Bot owner only)")]
        public static async Task ChangeBotUsername(CommandArgs e)
        {
            await Storage.client.CurrentUser.ModifyAsync(x => x.Username = e.ArgText);
        }

        [Command("avatar", CommandType.Admin), 
            Permission(Permission.ADMIN),
            Description("Change the bot's avatar.")]
        public static async Task ChangeAvatar(CommandArgs e)
        {
            try
            {
                var http = new HttpClient();
                var imageStream = await http.GetStreamAsync(e.ArgText);
                var memoryStream = new System.IO.MemoryStream();
                imageStream.CopyTo(memoryStream);
                imageStream.Dispose();
                memoryStream.Position = 0;

                await Storage.client.CurrentUser.ModifyAsync(x => x.Avatar = new Image(memoryStream));
            }
            catch (Exception) { }
        }
    }
}

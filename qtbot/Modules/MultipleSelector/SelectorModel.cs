﻿using System;
using System.Collections.Generic;
using System.Text;
using Discord;
using Discord.WebSocket;
using System.Threading.Tasks;

namespace qtbot.Modules.MultipleSelector
{
    class MultiSelector<T> : MultiSelector
    {
        //TODO: Allow the page to have multiple pages...
        //Probably do this by setting a limit of 10, then if theres more it shows an extra dialog
        //saying that you can switch page with "n" or "p" (next, previous)
        //use a 2 Dimensional list/array for that.
        //No need for extra parameters, maybe a pages boolean not needed, just limit the array to 10
        //When creating a selector.
        public static MultiSelector<T> Create(T[] PossibleReplyValues, IGuildUser Creator)
        {
            MultiSelector<T> x = new MultiSelector<T>()
            {
                PossibleReplyValues = PossibleReplyValues,
                Creator = Creator,
                messagesToDelete = new List<IMessage>(),
                actionToPerform = (z) =>
                {
                    byte o;
                    bool parsed = byte.TryParse(z.Content, out o);

                    if (o <= 0 || o > PossibleReplyValues.Length || !parsed)
                        return default(T);

                    return PossibleReplyValues[o - 1];
                }
            };
            return x;
        }

        public static MultiSelector<T> Create(T[] PossibleReplyValues, IGuildUser Creator, Func<IMessage, object> actionToPerform)
        {
            var x = new MultiSelector<T>()
            {
                PossibleReplyValues = PossibleReplyValues,
                Creator = Creator,
                messagesToDelete = new List<IMessage>(),
                actionToPerform = actionToPerform
            };
            return x;
        }


        public T[] PossibleReplyValues;
        public IGuildUser Creator;
        private Func<IMessage, object> actionToPerform;

        public override IGuildUser GetUser()
        {
            return Creator;
        }

        public override Func<IMessage, object> ReturnAction()
        {
            return actionToPerform;
        }

        public override void AddDeleteMessage(IMessage msg)
        {
            messagesToDelete.Add(msg);
        }

        public override Func<IMessage, object, Task> GetResponse()
        {
            return respondAction;
        }

        public override void SetResponse(Func<IMessage, object, Task> respondAction)
        {
            this.respondAction = respondAction;
        }

    }

    public abstract class MultiSelector
    {
        public abstract IGuildUser GetUser();
        public abstract Func<IMessage, object> ReturnAction();
        public abstract void AddDeleteMessage(IMessage msg);
        public abstract Func<IMessage, object, Task> GetResponse();
        public abstract void SetResponse(Func<IMessage, object, Task> a);

        public Func<IMessage, object, Task> respondAction;
        public List<IMessage> messagesToDelete;
        public bool canRespond = false;
    }
}

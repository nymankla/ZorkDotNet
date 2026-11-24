using Xunit;

namespace ZorkDotNet.Tests;

/// <summary>
/// Verb synonyms from dung.56 VSYNONYM, and the short verbs of act1.38/act2.27/act3.13/rooms.99 (Verbs class).
/// </summary>
public class VocabularyTests
{
    private static string Do(GameRunner runner, string command)
    {
        var mark = runner.GetOutput().Length;
        runner.Execute(command);
        return runner.GetOutputSince(mark);
    }

    [Theory]
    [InlineData("HOLD LEAFLET", "Taken.")]
    [InlineData("SKIM LEAFLET", "WELCOME TO DUNGEON")]
    [InlineData("LIST", "You are carrying:")]
    public void Synonyms_act_like_their_verb(string command, string expected)
    {
        using var runner = new GameRunner();
        runner.Execute("OPEN MAILBOX");
        if (command != "HOLD LEAFLET") runner.Execute("TAKE LEAFLET");

        Assert.Contains(expected, Do(runner, command));
    }

    [Fact]
    public void Pull_moves_the_rug()
    {
        using var runner = new GameRunner();
        runner.Teleport("LROOM");

        Assert.Contains("With a great effort, the rug is moved to one side of the room.", Do(runner, "PULL RUG"));
    }

    [Theory]
    [InlineData("EXAMINE LEAFLET")]
    [InlineData("LOOK AT LEAFLET")]
    public void Examining_something_readable_shows_its_text(string command)
    {
        using var runner = new GameRunner();
        runner.Run("OPEN MAILBOX", "TAKE LEAFLET");

        Assert.Contains("WELCOME TO DUNGEON", Do(runner, command));
    }

    [Fact]
    public void Examining_something_else_finds_nothing_special()
    {
        using var runner = new GameRunner();

        Assert.Contains("I see nothing special about the mailbox.", Do(runner, "EXAMINE MAILBOX"));
    }

    [Theory]
    [InlineData("BACK", "In any case, \"back\" doesn't work.")]
    [InlineData("REPENT", "It could very well be too late!")]
    [InlineData("FOO", "Well, FOO, BAR, and BLETCH to you too!")]
    [InlineData("CHOMP", "I don't know how to do that.  I win in all cases!")]
    [InlineData("SCREAM", "Aaaarrrrrrrrgggggggggggggghhhhhhhhhhhhhh!")]
    [InlineData("WIN", "Naturally!")]
    [InlineData("FROBOZZ", "The FROBOZZ Corporation created, owns, and operates this dungeon.")]
    [InlineData("SIGH", "You'll have to speak up if you expect me to hear you!")]
    [InlineData("ZORK", "That word is replaced henceforth with DUNGEON.")]
    [InlineData("DUNGEON", "At your service!")]
    [InlineData("GRANITE", "I think you are taking this thing for granite.")]
    [InlineData("GERONIMO", "Wasn't he an Indian?")]
    [InlineData("ULYSSES", "Wasn't he a sailor?")]
    [InlineData("BLAST", "I don't really know how to do that.")]
    [InlineData("TREASURE", "Nothing happens.")]
    [InlineData("PRAY", "If you pray enough, your prayers may be answered.")]
    [InlineData("HELLO MAILBOX", "I think that only schizophrenics say 'Hello' to a mailbox.")]
    public void Short_verbs_give_the_original_replies(string command, string expected)
    {
        using var runner = new GameRunner();

        Assert.Contains(expected, Do(runner, command));
    }

    [Theory]
    [InlineData("CURSE", new[] { "Such language in a high-class establishment like this!", "You ought to be ashamed of yourself.", "Its not so bad.  You could have been killed already.", "Tough shit, asshole.", "Oh, dear.  Such language from a supposed winning adventurer!" })]
    [InlineData("HI", new[] { "Hello.", "Good day.", "Nice weather we've been having lately", "How are you?", "Goodbye." })]
    [InlineData("HOP", new[] { "Very good.  Now you can go to the second grade.", "Have you tried hopping around the dungeon, too?", "Are you enjoying yourself?", "Wheeeeeeeeee!!!!!", "Do you expect me to applaud?" })]
    public void Random_replies_come_from_the_original_list(string command, string[] replies)
    {
        using var runner = new GameRunner();

        var output = Do(runner, command);

        Assert.Contains(replies, r => output.Contains(r, StringComparison.Ordinal));
    }

    [Fact]
    public void Treasure_and_temple_are_magic_words_between_the_two_rooms()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Teleport("TEMP1");

        Do(runner, "TREASURE");
        Assert.Equal("TREAS", runner.State.Here.Id);
        Do(runner, "TEMPLE");
        Assert.Equal("TEMP1", runner.State.Here.Id);
    }

    [Fact]
    public void Praying_at_the_altar_takes_you_to_the_forest()
    {
        using var runner = new GameRunner();
        runner.Teleport("TEMP2");

        Do(runner, "PRAY");
        Assert.Equal("FORE1", runner.State.Here.Id);
    }

    [Fact]
    public void Jumping_where_the_way_down_is_blocked_is_fatal()
    {
        using var runner = new GameRunner();
        runner.Teleport("CHAS1"); // DOWN: "Are you out of your mind?"

        var output = Do(runner, "JUMP");

        Assert.True(runner.State.AwaitingPatchAnswer); // JIGS-UP asks to patch you up
        Assert.Contains(new[] { "You should have looked before you leaped.", "I'm afraid that leap was a bit much for your weak frame.", "In the movies, your life would be passing in front of your eyes.", "Geronimo....." },
            r => output.Contains(r, StringComparison.Ordinal));
    }

    [Fact]
    public void Geronimo_in_the_barrel_goes_over_the_falls()
    {
        using var runner = new GameRunner();
        runner.Teleport("BARRE");

        Assert.Contains("Is this what 'over a barrel' means?", Do(runner, "GERONIMO"));
        Assert.True(runner.State.AwaitingPatchAnswer); // JIGS-UP asks to patch you up
    }

    [Fact]
    public void Saying_hello_to_the_sailor_wears_thin()
    {
        using var runner = new GameRunner();
        var sailor = new ZorkDotNet.Game.GameObject { Id = "SAILO", DescriptionShort = "sailor", Flags = ZorkDotNet.Game.ObjectFlags.Visible };
        runner.State.Here.Objects.Add(sailor);
        sailor.InRoom = runner.State.Here;

        for (var i = 1; i < 10; i++) Assert.Contains("Nothing happens here.", Do(runner, "HELLO SAILOR"));
        Assert.Contains("I think that phrase is getting a bit worn out.", Do(runner, "HELLO SAILOR"));
    }

    [Fact]
    public void Time_reports_how_long_the_game_has_been_played()
    {
        using var runner = new GameRunner();
        runner.State.StartedAt = runner.State.Clock.GetUtcNow() - TimeSpan.FromSeconds(3725);

        Assert.Contains("You have been playing DUNGEON for 1 hour, 2 minutes, and 5 seconds.", Do(runner, "TIME"));
    }
    [Theory]
    [InlineData("KNOCK ON DOOR", "I don't think that anybody's home.")]
    [InlineData("KNOCK AT MAILBOX", "Why knock on a mailbox?")]
    [InlineData("WAKE MAILBOX", "The mailbox isn't sleeping.")]
    [InlineData("DIG WITH LEAFLET", "Digging with a leaflet is silly.")]
    [InlineData("INFLATE MAILBOX", "How can you inflate that?")]
    [InlineData("DEFLATE MAILBOX", "Come on, now!")]
    [InlineData("TELL MAILBOX", "You cannot talk to that!")]
    [InlineData("WHERE MAILBOX", "There is a mailbox here.")]
    [InlineData("RING MAILBOX", "How, exactly, can I ring that?")]
    [InlineData("MELT MAILBOX", "I'm not sure that a mailbox can be melted.")]
    [InlineData("PLUG MAILBOX", "This has no effect.")]
    [InlineData("BRUSH LEAFLET", "If you wish, but I can't understand why??")]
    [InlineData("FIRST", "Nothing happens.")]
    [InlineData("HELP", "Useful commands")]
    [InlineData("VERSION", "This version created ")]
    public void Object_verbs_give_the_original_replies(string command, string expected)
    {
        using var runner = new GameRunner();
        runner.Give("ADVER");

        Assert.Contains(expected, Do(runner, command));
    }

    [Fact]
    public void Kicking_something_does_nothing_much()
    {
        string[] hoHum = [" does not seem to do anything.", " is not notably useful.", " isn't very interesting.", " doesn't appear worthwhile.", " has no effect.", " doesn't do anything."];
        using var runner = new GameRunner();

        var output = Do(runner, "KICK MAILBOX");

        Assert.Contains(hoHum, h => output.Contains("Munging a mailbox" + h, StringComparison.Ordinal));
    }

    [Fact]
    public void Find_says_where_something_is_inside_a_container()
    {
        using var runner = new GameRunner();
        runner.Execute("OPEN MAILBOX");

        var output = Do(runner, "FIND LEAFLET");

        Assert.Contains("There is a leaflet here." + Environment.NewLine + "It is in the mailbox, which is in the room.", output);
    }

    [Fact]
    public void Poking_a_villain_with_bare_hands_is_suicidal()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Teleport("MTROL");

        Assert.Contains("Munging a troll with your bare hands is suicidal.", Do(runner, "POKE TROLL"));
    }

    [Fact]
    public void Ringing_the_bell()
    {
        using var runner = new GameRunner();
        runner.Give("BELL");

        Assert.Contains("Ding, dong.", Do(runner, "RING BELL"));
    }

    [Theory]
    [InlineData("WHOUS", false)]
    [InlineData("DAM", true)]
    public void Swimming_is_not_allowed_in_the_water(string room, bool water)
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        runner.Teleport(room);

        var output = Do(runner, "SWIM");

        Assert.Equal(water, output.Contains("Swimming is not allowed in this dungeon.", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("LIGHT MATCH", "MATCH", "The match is now lit.")]
    [InlineData("STRIKE MATCH", "MATCH", "The match is now lit.")]
    [InlineData("LIGHT CANDLES", "CANDL", "The candles are now lit.")]
    public void Light_and_strike_light_things(string command, string objectId, string expected)
    {
        using var runner = new GameRunner();
        var obj = runner.Give(objectId);

        Assert.Contains(expected, Do(runner, command));
        Assert.True(obj.LightAmount > 0);
    }

    [Fact]
    public void Douse_puts_the_lamp_out()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;

        Assert.Contains("The lamp is now off.", Do(runner, "DOUSE LAMP"));
    }

    [Fact]
    public void Wait_lets_the_clock_run_three_more_moves()
    {
        using var runner = new GameRunner();
        runner.Give("LAMP").LightAmount = 1;
        var before = runner.State.LampTicks;

        Assert.Contains("Time passes...", Do(runner, "WAIT"));
        Assert.Equal(before - 4, runner.State.LampTicks); // three for WAIT, one for the turn itself
    }

    [Fact]
    public void Travel_is_a_way_of_saying_cross()
    {
        using var runner = new GameRunner();

        Assert.DoesNotContain("I don't know the word", Do(runner, "TRAVEL"));
    }
}

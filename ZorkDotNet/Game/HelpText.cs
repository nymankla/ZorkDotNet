namespace ZorkDotNet.Game;

/// <summary>
/// The text HELP prints (rooms.99 HELP: FILE-TO-TTY "MADADV" "HELP"), from madman/madadv.help in the MIT source.
/// Generated from that file: keep it exactly as it is there.
/// </summary>
public static class HelpText
{
    public const string Text =
        "Useful commands\n" +
        "\n" +
        "    The 'brief' command suppresses printing of long room descriptions\n" +
        "for rooms which have been visited.  The 'unbrief' command turns this\n" +
        "off.  The 'superbrief' command suppresses printing of long room\n" +
        "descriptions for all rooms; 'unsup' turns this off.\n" +
        "    The 'info' command prints a file which might give some idea of\n" +
        "what the game is about.\n" +
        "    The 'quit' command (or ctrl-G) prints your score, and asks whether\n" +
        "you wish to continue playing.\n" +
        "    The 'script', 'unscript', 'save', and 'restore' commands are\n" +
        "useful if you are a local user.  \n" +
        "Dungeon Command Parser\n" +
        "    \n" +
        "    A command is one line of text terminated by a carriage return.\n" +
        "For reasons of simplicity all words are distinguished by their first\n" +
        "five letters.  All others are ignored.  For example, your typing\n" +
        "'DISASSEMBLE THE ENCYLOPEDIA' while meaningless is also creating\n" +
        "excess effort for your fingers.  Note also that ambiguities can be\n" +
        "introduced by this:  'unscr' is 'UNSCRipt', not 'UNSCRew'.\n" +
        "\n" +
        "    You are talking to a fairly stupid parser, which understands the\n" +
        "following types of things.\n" +
        "\n" +
        " Actions:\t\n" +
        "    Among the more obvious of these, TAKE, DROP, etc.  Fairly general\n" +
        "    forms of these may be used:  PICK UP, PUT DOWN, etc.\n" +
        "\n" +
        " Directions:\n" +
        "    NORTH, SOUTH, UP, DOWN, etc. and their various abbreviations.\n" +
        "    Other more obscure directions (LAND, CLIMB) are appropriate in\n" +
        "    only certain situations.\n" +
        "    \n" +
        " Objects: \n" +
        "    Most objects have names, and can be referenced by them.\n" +
        "\n" +
        " Adjectives:\n" +
        "    Some adjectives are understood and are required when there are\n" +
        "    two objects which can be referenced with the same 'name' (e.g.\n" +
        "    DOORs, BUTTONs)\n" +
        "\n" +
        " Prepositions:\n" +
        "    It may be necessary in some cases to include prepositions, but\n" +
        "    the parser attempts to handle cases which aren't ambiguous\n" +
        "    without.  Thus 'Give car to demon' will work, as will 'Give demon\n" +
        "    car.'  'Give car demon' probably won't do anything interesting. \n" +
        "    When a preposition is used, it should be appropriate:  'Give car\n" +
        "    with demon' does not parse.\n" +
        "\n" +
        " Sentences:\n" +
        "\n" +
        "    The parser understands a reasonable number of things.  Rather\n" +
        "    than listing them, we advise you to do reasonable things.\n" +
        "\n" +
        " Ambiguity:\n" +
        "\n" +
        "    The parser tries to be clever about what to do in the case of\n" +
        "    actions which require objects in the case that the object is not\n" +
        "    specified.  If there is only one possible object, the parser will\n" +
        "    assume that it should be used.  Otherwise, the parser will ask.\n" +
        "    Most questions asked by the parser can be answered (e.g. With\n" +
        "    what?).\n" +
        "\t \n" +
        "  Inventory:  Lists the objects in your possession.\n" +
        "\n" +
        "  Look: Prints a description of your surroundings.\n" +
        "\n" +
        "  Containment:  \n" +
        "\n" +
        "     Some objects can contain other objects.  Many such containers can\n" +
        "     be opened and closed; the rest are always open.  They may or may\n" +
        "     not be transparent.  For you to access (take, for example) an\n" +
        "     object which is in a container, the container must be open; for you\n" +
        "     to see such an object, the container must either be open or\n" +
        "     transparent.  Containers have a capacity, and objects have sizes;\n" +
        "     the number of objects which will fit therefore depends on their\n" +
        "     sizes.  You may \"put\" any object you have access to (it need not be\n" +
        "     in your hands) into any other object; at some point, the program\n" +
        "     will attempt to pick it up if you don't already have it, which\n" +
        "     process may fail if you're carrying too much.  Although  containers\n" +
        "     can contain other containers, the program doesn't access more than\n" +
        "     one level down.\n" +
        "\n" +
        "  Fighting:\n" +
        "\n" +
        "     Occupants of the dungeon will, as a rule, fight back when\n" +
        "     attacked; they may in some cases attack you unprovoked.  Useful\n" +
        "     verbs here are 'attack <villain> with <weapon>', 'kill', etc. \n" +
        "     Knife-throwing may or may not be useful.  The adventurer has a\n" +
        "     fighting strength, which varies with time:  in particular, being\n" +
        "     in a fight, getting killed, and getting injured, all lower it. \n" +
        "     One's carrying capacity may also be reduced after a fight. \n" +
        "     Strength is regained with time.  (Thus, it is not a good idea to\n" +
        "     fight someone immediately after being killed.)  Other details\n" +
        "     may become apparent in the course of a few melees.  The\n" +
        "     'diagnose' command describes your state of health.\n";
}

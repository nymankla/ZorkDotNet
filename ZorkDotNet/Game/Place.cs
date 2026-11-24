namespace ZorkDotNet.Game;

/// <summary>
/// Where an object is: on the floor of a room (MDL OROOM), carried by an adventurer (AOBJS), or inside another object
/// (OCAN). An object's place changes only through <see cref="GameObject.MoveTo"/>, which keeps the place's list of
/// objects in step with it.
/// </summary>
public union Place(Room, Adventurer, GameObject);

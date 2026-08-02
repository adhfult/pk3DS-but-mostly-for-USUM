import std.string;
import std.mem;

//READ ME:
//This pattern is made by Omikaye, who is NOT proficient with making patterns.
//So it is NOT foolproof at identifying each structure properly 100% of the time.
 
//The comments also contain what I've learned from this, they might help you if you try to find certain values mean. 
//I will update this pattern as I learn more. 
//If you have a way to make this pattern better, please let me know on discord so that I may update it for others.
//Please view the comments in Pattern Data and read them to make sure the data matches properly.


struct TrainerCoords {
    float trainerX, trainerY, trainerZ;
};
struct TrainerQuaternion {
 float trainerRotationW, trainerRotationX, trainerRotationY, trainerRotationZ;
 };
struct Trainer {
    std::mem::AlignTo<4>;
    u32 trainerObjectType [[color("CB0F0F"), comment("Trainer Object Type. These are 84 bytes long. '07 00 00 00' Is trainer type. Do not alter this value.")]];
    TrainerCoords [[color("F15808"), comment("Default coordinates of the Trainer. They should match a set from the item list down below. Make sure to edit these if you change the corresponding trainer coordinates. This is simply the default starting position of the trainer. Does not change their walk paths or animation. If you change this without changing the animation and walk paths, you may see them walk through walls.")]];
    TrainerQuaternion [[color("CDC420"), comment("Trainer DEFAULT rotation and facing direction. This wont alter their 'sightlines', animation or walking paths, but it does change where they are looking when loaded in. This is the starting point for an animation set. Quaternions are complicated. (Well, for me, I'm not a mathmatician) I suggest using a calculator if you don't get it.")]]; 
    u32 trainerRomVersion [[color("494848"), comment("Idk. Further testing required. Generally empty.")]];
    u32 trainerFlagwork [[color("494848"), comment("Idk. Further testing required. Generally empty.")]];
    u32 trainerFlagworknum [[color("494848"), comment("Idk. Further testing required. Generally empty.")]];
    u32 trainerEventID [[color("536b06"), comment("A unique index. Don't touch. If adding a new trainer, increment by 1. It always starts at '101' (or '65' in hex) ")]];
    u32 trainerModelID [[color("1144CE"), comment("The Trainer's OVERWORLD model This is the model of the NPC outside of battle. Changing this will affect what they look like. The model MUST ALREADY EXIST on this map. Ex. Hau'oli cemetery does not have the model for Lillie or an Ace Trainer, so you couldn't use either to replace a trainer there. You could turn them all into Office Workers if you wanted though. DOES NOT affect the trainer itself when you enter battle, nor the battle model. That is handled by the next few bytes.")]];
    u32 trainerBattlescriptID [[color("6207D7"), comment("This is the trainer's ID. Kind of. Its actually the trainer's number + 1000. Ex. Preschooler Malia is trainer 468. Here, the value is 1468, or BC 05. Changing this does indeed change the trainer that appears and fights you DURING THE BATTLE. Does NOT affect overworld model. DOES affect the trainer's text when they spot you or you interact with them.")]];
    u32 trainerAlias [[color("494848"), comment("Idk. Usually empty.")]];
    u32 trainerMoveDataOffset [[color("5356DA"), comment("Offset to movement/motion data. Offsets are from the start of the trainer section. Starting from the beginning of the areaCount header.")]];
    u32 trainerTalkCollisionOffset [[color("C6A432"), comment("Offset to talk interaction collision")]];
    u32 trainerCollisionOffset [[color("DDD931"), comment("Offset to main collision geometry")]];
    u32 trainerSignalOffset [[color("CAB3BE"), comment("Offset to signal data. This is trainer sightline. If there is no value here, try changing the offset to match a trainer on this route that has one. '02 00 00 00' is a working signal offset.")]];
    u32 trainerPathCoreOffset [[color("7684A5"), comment("Offset to path/patrol data.")]];
    u32 trainerPathPointOffset [[color("B4B4B5"), comment("Offset to waypoint/relay point data")]];
   
    //Trainer movement and sightlines is still being researched...
    //They are changable, and will appear below the trainers themselves, but the byte length is not static like the rest of these. Whether a trainer moves, is stationary, or looks around will all affect length.
    //You can still mess with these values, but I cannot mark them just yet as they are all over the place, but here is what I have so far.
    
    //If you'd like to try changing these, try matching up the coordinates from the trainers with the sightlines. Just be very careful not to bleed into the next trainer. Trainers that move will have multiple coordinate sets, but the first one will match their defualt coords from up above. They may even have multiple of the same coordinates if they return back to their original location. Check carefully. (Note: You COULD change all of their movement coordinates to the same one in order to make them stationary, but they will still perform their animations and movement. If you set their coordinates to only one spot and they are coded to move, it WILL crash your game. Simply set their second coordinate to a pixel or two off and they will still essentially "spin in place"  
    
    //"08" is the objecttype for trainers that move. "04" is used for unmoving trainers. "Unmoving" here just means they don't walk around, they might still have looking back and forth animations. 04 is the same one used for pokeball collision, so I'm assuming its just a standard circular collision box.
    //Next thing you'll probably see is coordinates. Self explanatory.
    //Next you will see either "00 00 A0 42 00 00 16 43"(Mobile trainers) or "00 00 20 42 00 00 16 43"(Stationary trainers). Not sure what this is for exactly, but the Item collision had these as well. I wouldn't touch them, but they appear after every set of coordinates, so they are helpful for locating each coord set.
    //Now here's where things are kind of different for every trainer. Sight range is in here. But I still havent found a consistent way to find it. Now, I don't want to advocate for "Just mess with values until it works" buuut thats kind of what I've been doing so... Until I have more thats all I got. It is likely just a u32 and the value is often between 80-200 by default so hopefully that helps.
    //The animations are also here. I havent Identified every animation set so far just yet, but "5E 00" is the animation for "Stand still and throw pokeball up and down." For trainers with multiple animation sets, like mobile trainers, these animations will have a number 8 bytes after the animation. This is the number of times the animation is performed before they move on to the next one. So if they have "5E 00" then another animation, you can make them stand there and do that animation X times, before they continue with their other movement cycles. Looking into this more going forwards.
    //Non stationary animations will have coordinates following it BEFORE the animation quantity, so it can make them harder to spot, but this affects where they are looking during the animation (I think)
    
    
};



u16 zoneHeader @ 0x00 [[color("4AA848"), comment("This is the header of the trainer document. It always starts with '45 54'")]];
u8 areaCount @ 0x02 [[color("4CF838"), comment("This number is the number of zones there are, and thus the number of sections in this file. It will usually be a 1, but some areas, like Route 1 has multiple zones and can be a 2 or 3 instead. Do not change this.")]];

u8 a1;
u8 a2;
u8 a3;
u8 a4;
u8 b1;
u8 b2;

if (areaCount == 1){
a1 = 0x04;
a2 = 0x04;
a3 = 0x04;
a4 = 0x04;
b1 = 0x08;
b2 = 0x0C;

}
if (areaCount == 2){
a1 = 0x04;
a2 = 0x08;
a3 = 0x04;
a4 = 0x04;
b1 = 0x0C;
b2 = 0x0F;

}
if (areaCount == 3){
a1 = 0x04;
a2 = 0x08;
a3 = 0x0C;
a4 = 0x04;
b1 = 0x0F;
b2 = 0x14;

}
if (areaCount == 4){
a1 = 0x04;
a2 = 0x08;
a3 = 0x0C;
a4 = 0x0F;
b1 = 0x14;
b2 = 0x18;

}
u32 section1Start @ a1 [[color("4CF838"), comment("This is the byte count before the start of the current area's section. Should not be changed.")]];
u32 section2Start @ a2 [[color("4CF838"), comment("This is the byte count before the start of the current area's section. Should not be changed.")]];
u32 section3Start @ a3 [[color("4CF838"), comment("This is the byte count before the start of the current area's section. Should not be changed.")]];
u32 section4Start @ a4 [[color("4CF838"), comment("This is the byte count before the start of the current area's section. Should not be changed.")]];
u32 TotalSize @ b1 [[color("7FF8ED"), comment("This is the total size of the trainer file in bytes. Includes all zones. This needs to be updated if you add any data.")]];
u32 NumofEntries @ b2 [[color("78CAAA"), comment("This is the number of trainer entries there are in this area segment. Each area in a zone will have this. This will need to increase if you add any data.")]];

std::mem::MagicSearch<"\x07\x00\x00\x00", Trainer> Trainer @ $;



return areaCount * 0;
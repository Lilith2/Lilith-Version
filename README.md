
#### 3/17/2025

Dumper -> added streamer mode from Butter's new commit on github. Removed infinite stam, daytime feature related structs from dumper.

Client -> Removed Infinite Stamina, Daytime feature code. Removed Menu options for this too.
#### 3/25/2025

###### Client Changes
- Player.cs added the following function for use in player height draw function private int RoundToMultipleOfThree(float number). Up and Down arrows drawn on players re-coded for accuracy of 3 meter increments. If height is 0 to 2 inclusive, no arrow drawn.

- Config.cs refactored GetRandomBones() to have less bias in the generations. I was getting an insane head bias on anything 15 or higher but couldn't replicate it with other hitboxes. The instance version of C# Random has bias according to Claude 3.7 Sonnet. So it told me to use Random.Shared.Next instead which creates a new random every call I think? Not 100% sure.

- Menu changed to no longer show options I don't like. (all the byte patch shit except silent aim and streamer mode) 

- Menu aimline and ESP distance inputs changed to keyboard input instead of inaccurate sliders

- Removed Wide Lean from the Menu, I think I kept the code in the Client.

#### 3/26/2025

###### Client Changes
- Ported Zero from EER's no byte patch silent aim into my Client
- Commented out old byte patch silent aim to keep because it's fucking cool. Most unique solution to silent aim I've ever seen externally but ultimately quite easy to detect.
- Removed menu options for web radar because I don't even have the code to work it.
- Removed all unused menu options
- 

###### Big changes to Byte Patch code
I removed everything that does byte patching from the menu except weapon malfunctions and silent aim. Both have the code still but no way to enable it without un-commenting stuff or re-adding it to menu.

#### 3/28/2025

###### Client Changes
- adding new maps, I think I have to update the coordinates for the maps how the code uses it. Dreadful said he inverted all the maps???
- change the market prices from PVE to PVP? Not sure how yet. Dreadful said his readme has a hint.
- add paper note extract radar esp & fuser esp
	- Exfil.cs line 41
	- Exfil.cs line186
	- Exfile.cs line 235


###### Dumper Changes
- Add paper note extracts offsets EFTProcessor.cs
	- line 907 Array of exfils dump object
	- line 926 specific array offset

#### 3/30/2025
Made a notepad++ plugin to convert dump.txt into sdk.cs readable format.
//link here

#### 4/1/2025
###### Client Changes
- added all new market data code from Dreadful's fork manually into this version. Seems to work. It also added a loading bar to startup.


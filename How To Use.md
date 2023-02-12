# How To Unpack NPK Files

### 1. Download and install both MwareKeyFinder.rar and NPK3Tool.rar and extract them.
    If key is already included in the NPK3Tool, skip Steps 2-3
### 2. Navigate to your game's exe file and drag and drop it onto the MwareKeyFinder tool (SRLInjector.exe).
### 3. You will receive a message showing you the key for the game. 
    Copy and paste the message into an empty text file.
    Remove the "0x" and commas manually.
    Save the remaining text and this will be the key to extract the game.
### 4. Getting the version of the engine the game is using(Optional).
    Only needed for repacking.
    Open the npk file in a hex editor. Check the decoded text for the first 4 characters in the file.
    The number after "NPK" (2 or 3) is the engine's version.
### 5. Drag the npk file you want to extract onto the NPK3Tool.exe file.
    Options will pop up so select the option corresponding to your game. If it is not there, type 11.
### 6. If asked for the key, copy/paste the key.
### 7. Select between SJIS and UTF-8 encoding.
    Most of the time UTF-8 should be selected. If text is not correct, select the other option.
### 8. The npk file's contents will be extracted into a new folder.
    Folder will end with a "~".

# How To Repack NPK Files

### 1. If editing the files inside the new folder, replace those original files with the new ones.
    The following process is similar to the previous process(Steps 5-7).
### 2. Drag and drop the folder from Step 8 onto the NPK3Tool.exe file.
    Re-enter the key and the encoding.
    A new folder will be created with the folder's name but ending with "_new".

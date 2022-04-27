# Smali Consolidator
A command line tool created in .NET 6.0 using Visual Studio 2022 to consolidate two folders containing .smali files.


Copies non-existing .smali files from one directory and puts them in the correct places in the other directory.

Takes different functions/field/annotations/etc. from one .smali file (assuming the file exists at the same location in both directories) and copies them over.


Assumes APKTool was used for the decompilation of the APK into .smali.

## Usage
Enter the first folder where the .smali files are that contain the differences you want to apply. (read)

Enter the second folder where the .smali files are that you want to apply the changes to. (write)

Press enter and the tool will do its thing.

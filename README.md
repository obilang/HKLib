A fork from the HKLib

# Changes of this Fork
* Binary read part support havok 2020 file (not support 2018 any more) (Only tested on files from FF16)
* Binary write part not work yet
* Added support for decode hkaPredictiveCompressedAnimation (this part should be work generally if you can deserilize any havok file and get related data)
* (I do not have a HavokTypeRegistry xml for 2020, all the type defination are come from the binary file itself and hacked into the HavokTypeRegistry)



# HKLib
A WIP Library for Reading/Writing Havok Files. Currently supports Havok 2018. This is an early release, the API may change considerably in future versions.

# Special Thanks
* [Skyth](https://github.com/blueskythlikesclouds) - Reverse engineered Havok 2016 tagfiles and created TagTools
* [GoogleBen](https://github.com/googleben) - Reverse engineered Havok 2018 tagfiles
* [Katalash](https://github.com/katalash) - Created HKX2 which inspired HKLib
* [TKGP](https://github.com/JKAnderson) - For his BinaryWriter/Reader implementations

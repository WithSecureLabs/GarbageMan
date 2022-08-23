#if DBG
#define MODULE_BUILD_CONFIGURATION "(debug)"
#else
#define MODULE_BUILD_CONFIGURATION
#endif

#if defined(_WIN64)
#define MODULE_BUILD_PLATFORM "64-bit"
#else
#define MODULE_BUILD_PLATFORM "32-bit"
#endif // _WIN64

#define CLIENT_FILEDESCRIPTION "F-Secure Process Nofify " MODULE_BUILD_PLATFORM " " MODULE_BUILD_CONFIGURATION "\0"
#define CLIENT_INTERNALNAME "psnotify\0"
#define CLIENT_ORIGINALFILENAME "psnotify.sys\0"

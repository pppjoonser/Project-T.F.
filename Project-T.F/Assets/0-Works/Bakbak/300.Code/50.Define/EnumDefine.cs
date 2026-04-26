using System;
using UnityEngine;

[Flags]
public enum LogTag : ulong //type of Log
{
    DEFAULT = 1,
    MESSAGE = 1<<1,
    TODO = 1<<2,
    TEMP = 1<<3,

    INIT=1<<4,
    RUN = 1 <<5,
    FILE_LOAD=1<<6,
    FILE_UNLOAD = 1 << 7,
    LOGMANAGER = 1 << 8, //log manager error <- must not happen

    DATA_MISSING=1 <<9,
    FILE_MISSING = 1 << 10,
    VALUENULL= 1 << 11,

    WARNING = 1 << 12,
    CRITICAL = 1 << 13,
}
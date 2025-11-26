// 

using System;
using Unity.Mathematics;

namespace DevDev.LDP.Input
{
    public struct Sample_Pawn
    {
        internal float2 moveDir;
        internal bool jumpTriggered;
        internal bool jumpValue;
        internal bool fallthrough;
    }

    public struct Sample_General
    {
        internal bool pause;
        internal bool fullScreen;
        internal bool devMode;
    }
}

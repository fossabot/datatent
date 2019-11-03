﻿using System;
using System.Collections.Generic;
using System.Text;
using Datatent.Core.Pages;
#pragma warning disable CA1802 // Use literals where appropriate

namespace Datatent.Core
{
    internal sealed class Constants
    {
        public static readonly uint PAGE_SIZE = 64000;

        public const int PAGE_HEADER_SIZE = 92;

        public static readonly uint PAGE_SIZE_INCL_HEADER = PAGE_HEADER_SIZE + PAGE_SIZE;

        public static readonly uint PAGES_PER_DATA_BLOCK = 200;

        public static readonly uint DATA_BLOCK_SIZE = PAGES_PER_DATA_BLOCK * PAGE_SIZE_INCL_HEADER;

        public static readonly uint BLOCK_SIZE = DATA_BLOCK_SIZE;

        public static readonly uint DATABASE_HEADER_SIZE = 2048;

        public const int BLOCK_HEADER_SIZE = 92;

        public static readonly uint DATA_BLOCK_SIZE_INCL_HEADER = BLOCK_HEADER_SIZE + DATA_BLOCK_SIZE;

        public static readonly uint BLOCK_SIZE_INCL_HEADER = DATA_BLOCK_SIZE_INCL_HEADER;
        
        public static readonly uint DOCUMENT_HEADER_SIZE = 32;
    }
}
#pragma warning restore CA1802 // Use literals where appropriate

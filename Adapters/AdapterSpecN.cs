﻿// Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Myro.Adapters
{
    interface AdapterTokenN
    {
        string Name { get; }
        IAdapter AdapterIfAttached { get; }
    }
}

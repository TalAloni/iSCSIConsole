/* Copyright (C) 2012-2024 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;

namespace ISCSI.Server
{
    public class TextRequestArgs : EventArgs
    {
        public List<KeyValuePair<string, string>> RequestParaemeters;
        public List<KeyValuePair<string, string>> ResponseParaemeters = new List<KeyValuePair<string, string>>();

        public TextRequestArgs(List<KeyValuePair<string, string>> requestParaemeters)
        {
            RequestParaemeters = requestParaemeters;
        }
    }
}

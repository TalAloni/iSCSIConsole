/* Copyright (C) 2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */

namespace ISCSI
{
    /// <summary>
    /// Default operational parameters for iSCSI session / connection, as specified in RFC 3720.
    /// </summary>
    public class DefaultParameters
    {
        public class Session
        {
            /// <summary>
            /// The maximum number of connections per session.
            /// </summary>
            public const int MaxConnections = 1;

            /// <summary>
            /// Allow the initiator to start sending data to a target as if it has received an initial R2T
            /// </summary>
            public const bool InitialR2T = true;

            public const bool ImmediateData = true;

            /// <summary>
            /// The total of all the DataSegmentLength of all PDUs in a sequence MUST not exceed MaxBurstLength.
            /// Maximum SCSI data payload in bytes in a Data-In or a solicited Data-Out iSCSI sequence (i.e. that belongs to a single command).
            /// Irrelevant to the target in general, the initiator instructs us using ExpectedDataTransferLength.
            /// </summary>
            public const int MaxBurstLength = 262144;

            /// <summary>
            /// The total of all the DataSegmentLength of all PDUs in a sequence MUST not exceed FirstBurstLength for unsolicited data.
            /// Maximum amount in bytes of unsolicited [SCSI] data an iSCSI initiator may send to the target during the execution of a single SCSI command.
            /// Irrelevant to the target in general, irrelevant when (InitialR2T = Yes and) ImmediateData = No.
            /// </summary>
            public const int FirstBurstLength = 65536;

            /// <summary>
            /// Minimum time, in seconds, to wait before attempting an explicit/implicit logout after connection termination / reset.
            /// </summary>
            public const int DefaultTime2Wait = 2;

            /// <summary>
            /// Maximum time, in seconds after an initial wait (Time2Wait), before which an active task reassignment
            /// is still possible after an unexpected connection termination or a connection reset.
            /// </summary>
            public const int DefaultTime2Retain = 20;

            public const int MaxOutstandingR2T = 1;

            public const bool DataPDUInOrder = true;

            public const bool DataSequenceInOrder = true;

            public const int ErrorRecoveryLevel = 0;
        }

        public class Connection
        {
            /// <summary>
            /// Maximum data segment length that the target or initator can receive in an iSCSI PDU.
            /// Per direction parameter that the target or initator declares.
            /// The default MaxRecvDataSegmentLength is used during Login.
            /// </summary>
            public const int MaxRecvDataSegmentLength = 8192;
        }
    }
}

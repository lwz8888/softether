﻿// CoreUtil
// 
// Copyright (C) 2012-2014 Daiyuu Nobori. All Rights Reserved.
// Copyright (C) 2012-2014 SoftEther VPN Project at University of Tsukuba. All Rights Reserved.
// Comments: Tetsuo Sugiyama, Ph.D.
// 
// 
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// version 2 as published by the Free Software Foundation.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License version 2
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA 02111-1307, USA.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
// CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
// TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
// SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// 
// THE LICENSE AGREEMENT IS ATTACHED ON THE SOURCE-CODE PACKAGE
// AS "LICENSE.TXT" FILE. READ THE TEXT FILE IN ADVANCE TO USE THE SOFTWARE.
// 
// 
// THIS SOFTWARE IS DEVELOPED IN JAPAN, AND DISTRIBUTED FROM JAPAN,
// UNDER JAPANESE LAWS. YOU MUST AGREE IN ADVANCE TO USE, COPY, MODIFY,
// MERGE, PUBLISH, DISTRIBUTE, SUBLICENSE, AND/OR SELL COPIES OF THIS
// SOFTWARE, THAT ANY JURIDICAL DISPUTES WHICH ARE CONCERNED TO THIS
// SOFTWARE OR ITS CONTENTS, AGAINST US (SOFTETHER PROJECT, SOFTETHER
// CORPORATION, DAIYUU NOBORI OR OTHER SUPPLIERS), OR ANY JURIDICAL
// DISPUTES AGAINST US WHICH ARE CAUSED BY ANY KIND OF USING, COPYING,
// MODIFYING, MERGING, PUBLISHING, DISTRIBUTING, SUBLICENSING, AND/OR
// SELLING COPIES OF THIS SOFTWARE SHALL BE REGARDED AS BE CONSTRUED AND
// CONTROLLED BY JAPANESE LAWS, AND YOU MUST FURTHER CONSENT TO
// EXCLUSIVE JURISDICTION AND VENUE IN THE COURTS SITTING IN TOKYO,
// JAPAN. YOU MUST WAIVE ALL DEFENSES OF LACK OF PERSONAL JURISDICTION
// AND FORUM NON CONVENIENS. PROCESS MAY BE SERVED ON EITHER PARTY IN
// THE MANNER AUTHORIZED BY APPLICABLE LAW OR COURT RULE.
// 
// USE ONLY IN JAPAN. DO NOT USE IT IN OTHER COUNTRIES. IMPORTING THIS
// SOFTWARE INTO OTHER COUNTRIES IS AT YOUR OWN RISK. SOME COUNTRIES
// PROHIBIT ENCRYPTED COMMUNICATIONS. USING THIS SOFTWARE IN OTHER
// COUNTRIES MIGHT BE RESTRICTED.
// 
// 
// SOURCE CODE CONTRIBUTION
// ------------------------
// 
// Your contribution to SoftEther VPN Project is much appreciated.
// Please send patches to us through GitHub.
// Read the SoftEther VPN Patch Acceptance Policy in advance:
// http://www.softether.org/5-download/src/9.patch
// 
// 
// DEAR SECURITY EXPERTS
// ---------------------
// 
// If you find a bug or a security vulnerability please kindly inform us
// about the problem immediately so that we can fix the security problem
// to protect a lot of users around the world as soon as possible.
// 
// Our e-mail address for security reports is:
// softether-vpn-security [at] softether.org
// 
// Please note that the above e-mail address is not a technical support
// inquiry address. If you need technical assistance, please visit
// http://www.softether.org/ and ask your question on the users forum.
// 
// Thank you for your cooperation.


using System;
using System.Threading;
using System.Data;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Text;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Web.UI.HtmlControls;
using System.IO;
using System.IO.Compression;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using CoreUtil.Internal;

namespace CoreUtil
{
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct GZipHeader
	{
		public byte ID1, ID2, CM, FLG;
		public uint MTIME;
		public byte XFL, OS;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct GZipFooter
	{
		public uint CRC32;
		public uint ISIZE;
	}

	public static class GZipUtil
	{
		public static byte[] Decompress(byte[] gzip)
		{
			using (GZipStream stream = new GZipStream(new MemoryStream(gzip), CompressionMode.Decompress))
			{
				const int size = 4096;
				byte[] buffer = new byte[size];
				using (MemoryStream memory = new MemoryStream())
				{
					int count = 0;
					do
					{
						count = stream.Read(buffer, 0, size);
						if (count > 0)
						{
							memory.Write(buffer, 0, count);
						}
					}
					while (count > 0);
					return memory.ToArray();
				}
			}
		}
	}

	public class GZipPacker
	{
		Fifo fifo;
		ZStream zs;
		long currentSize;
		uint crc32;
		bool finished;

		public bool Finished
		{
			get { return finished; }
		}

		public Fifo GeneratedData
		{
			get
			{
				return this.fifo;
			}
		}

		public GZipPacker()
		{
			fifo = new Fifo();

			zs = new ZStream();
			zs.deflateInit(-1, -15);

			this.currentSize = 0;
			this.crc32 = 0xffffffff;
			this.finished = false;

			GZipHeader h = new GZipHeader();
			h.ID1 = 0x1f;
			h.ID2 = 0x8b;
			h.FLG = 0;
			h.MTIME = Util.DateTimeToUnixTime(DateTime.Now.ToUniversalTime());
			h.XFL = 0;
			h.OS = 3;
			h.CM = 8;

			fifo.Write(Util.StructToByte(h));
		}

		public void Write(byte[] data, int pos, int len, bool finish)
		{
			byte[] srcData = Util.ExtractByteArray(data, pos, len);
			byte[] dstData = new byte[srcData.Length * 2 + 100];

			if (this.finished)
			{
				throw new ApplicationException("already finished");
			}

			zs.next_in = srcData;
			zs.avail_in = srcData.Length;
			zs.next_in_index = 0;

			zs.next_out = dstData;
			zs.avail_out = dstData.Length;
			zs.next_out_index = 0;

			if (finish)
			{
				zs.deflate(zlibConst.Z_FINISH);
			}
			else
			{
				zs.deflate(zlibConst.Z_SYNC_FLUSH);
			}

			fifo.Write(dstData, 0, dstData.Length - zs.avail_out);

			currentSize += len;

			this.crc32 = ZipUtil.Crc32Next(data, pos, len, this.crc32);

			if (finish)
			{
				this.finished = true;
				this.crc32 = ~this.crc32;

				GZipFooter f = new GZipFooter();
				f.CRC32 = this.crc32;
				f.ISIZE = (uint)(this.currentSize % 0x100000000);

				fifo.Write(Util.StructToByte(f));
			}
		}
	}
}

// Developed by SoftEther VPN Project at University of Tsukuba in Japan.
// Department of Computer Science has dozens of overly-enthusiastic geeks.
// Join us: http://www.tsukuba.ac.jp/english/admission/
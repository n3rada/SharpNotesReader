using System;
using System.IO;
using System.Text;

namespace SharpNotesReader
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Set console output encoding to UTF-8 to handle a wider range of characters
            Console.OutputEncoding = Encoding.UTF8;

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string tabStatePath = Path.Combine(localAppData, @"Packages\Microsoft.WindowsNotepad_8wekyb3d8bbwe\LocalState\TabState");

            if (!Directory.Exists(tabStatePath))
            {
                Console.WriteLine("[!] TabState directory not found.");
                return;
            }

            foreach (string filePath in Directory.EnumerateFiles(tabStatePath, "*.bin"))
            {
                // Skip .0.bin and .1.bin files
                if (filePath.EndsWith(".0.bin") || filePath.EndsWith(".1.bin"))
                {
                    continue;
                }

                Console.WriteLine($"[*] Processing File: {Path.GetFileName(filePath)}");

                try
                {
                    using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        byte[] fileBytes = new byte[fs.Length];
                        fs.Read(fileBytes, 0, fileBytes.Length);

                        // Verify the NP signature
                        if (fileBytes.Length < 2 || fileBytes[0] != 0x4E || fileBytes[1] != 0x50)
                        {
                            Console.WriteLine("[-] File does not have the correct NP signature.");
                            continue;
                        }

                        using (MemoryStream ms = new MemoryStream(fileBytes))
                        using (BinaryReader reader = new BinaryReader(ms))
                        {
                            // Skip first 2 bytes for "NP" (Magic Byte)
                            reader.BaseStream.Seek(2, SeekOrigin.Begin);

                            ulong sequenceNumber = ReadULEB128(reader);

                            // Read the flag (4th byte)
                            ulong flag = ReadULEB128(reader);

                            if (flag == 0)
                            {
                                Console.WriteLine("|-> Type: Untitled Note");

                                byte delmiter = reader.ReadByte();

                                ulong selectionStartIndex = ReadULEB128(reader);
                                ulong selectionEndIndex = ReadULEB128(reader);

                                byte wordWrap = reader.ReadByte();
                                byte rightToLeft = reader.ReadByte();
                                byte showUnicode = reader.ReadByte();

                                ulong optionCount = ReadULEB128(reader);
                                byte[] options = reader.ReadBytes((int)optionCount);

                                // Read content length
                                ulong contentLength = ReadULEB128(reader);

                                Console.WriteLine($"|-> Content Length: {contentLength} bytes");

                                // Double the bytes to read for UTF-16LE
                                byte[] contentBytes = reader.ReadBytes((int)contentLength * 2);

                                // Decode the content as UTF-16LE
                                // Replace \r with \n for proper formatting
                                string content = Encoding.Unicode.GetString(contentBytes).Replace("\r", "\n");

                                // Print the content
                                Console.WriteLine("=== Note Content ===");
                                Console.WriteLine(content);
                                Console.WriteLine("====================");

                            }
                            else if (flag == 1)
                            {
                                Console.WriteLine("|-> Type: Saved File");

                                // Read the path length
                                ulong pathLength = ReadULEB128(reader);

                                // Double the bytes to read for UTF-16LE
                                byte[] pathBytes = reader.ReadBytes((int)pathLength * 2);

                                // Decode the content as UTF-16LE
                                string decodedPath = Encoding.Unicode.GetString(pathBytes);

                                Console.WriteLine($"|-> Path: {decodedPath}");

                                ulong contentLength = ReadULEB128(reader);

                                Console.WriteLine($"|-> Content Length: {contentLength} bytes");

                                byte encoding = reader.ReadByte();
                                // Console.WriteLine($"|-> Encoding: 0x{encoding:X2}");

                                byte carriageReturnType = reader.ReadByte();
                                
                                ulong timestamp = ReadULEB128(reader);
                                DateTime dateTime = DateTime.FromFileTime((long)timestamp);
                                Console.WriteLine($"|-> File time: {dateTime:yyyy-MM-dd HH:mm:ss}");

                                byte[] fileHash = reader.ReadBytes(32);

                                byte[] delim1 = reader.ReadBytes(2); //Unknown / Delimiter / 0x00 0x01

                                ulong selectionStartIndex = ReadULEB128(reader);
                                ulong selectionEndIndex = ReadULEB128(reader);


                                byte wordWrap = reader.ReadByte();

                                byte rightToLeft = reader.ReadByte();

                                byte showUnicode = reader.ReadByte();

                                ulong optionCount = ReadULEB128(reader);

                                byte[] options = reader.ReadBytes((int)optionCount);

                                contentLength = ReadULEB128(reader);

                                // Double the bytes to read for UTF-16LE
                                byte[] contentBytes = reader.ReadBytes((int)contentLength * 2);

                                // Decode the content as UTF-16LE
                                // Replace \r with \n for proper formatting
                                string content = Encoding.Unicode.GetString(contentBytes).Replace("\r", "\n");

                                // Print the content
                                Console.WriteLine("=== Note Content ===");
                                Console.WriteLine(content);
                                Console.WriteLine("====================");
                            }
                            else
                            {
                                Console.WriteLine("[-] Unknown TypeFlag encountered.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Error reading file '{Path.GetFileName(filePath)}': {ex.Message}");
                }

                Console.WriteLine();  // Add a blank line between file outputs
            }
            // Console.ReadKey();
        }

        /// <summary>
        /// Reads a uLEB128 encoded ulong from the binary stream.
        /// This is a variable-length encoding, you can't know in advance how many bytes each ReadULEB128 call consumes.
        /// </summary>
        /// <param name="reader">The binary reader to read from.</param>
        /// <returns>The decoded unsigned ulong value.</returns>
        /// <exception cref="EndOfStreamException">Thrown if the end of the stream is reached unexpectedly.</exception>
        static ulong ReadULEB128(BinaryReader reader)
        {
            ulong value = 0;
            int shift = 0;
            bool more;

            do
            {
                if (shift >= 64)
                {
                    throw new FormatException("ULEB128 sequence is too long for a 64-bit integer.");
                }

                // Read the next byte
                byte next = reader.ReadByte();

                // Get the next chunk of data, mask the highest bit as it's used to indicate continuation
                ulong chunk = (ulong)(next & 0x7F);

                // Combine this chunk with the accumulated value
                value |= chunk << shift;

                // Check if there's more data (if the highest bit is set)
                more = (next & 0x80) != 0;

                // Shift to process the next chunk
                shift += 7;
            }
            while (more);

            return value;
        }

    }
}

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
                                Console.WriteLine($"|-> Encoding: 0x{encoding:X2} ({GetEncodingName(encoding)})");

                                byte carriageReturnType = reader.ReadByte();
                                
                                ulong timestamp = ReadULEB128(reader);
                                if (timestamp == 0)
                                {
                                    Console.WriteLine("|-> Status: File was opened but not edited. Skipping further parsing.");
                                    continue;
                                }
                                
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
        /// Reads an unsigned LEB128 (ULEB128) encoded 64-bit integer from the given BinaryReader.
        /// This encoding is variable-length and efficient for small values.
        /// </summary>
        /// <param name="reader">The binary reader to read from.</param>
        /// <returns>The decoded ulong value.</returns>
        /// <exception cref="FormatException">Thrown if the value exceeds 64 bits.</exception>
        /// <exception cref="EndOfStreamException">Thrown if the stream ends unexpectedly during decoding.</exception>
        static ulong ReadULEB128(BinaryReader reader)
        {
            ulong result = 0; // Final value to be constructed
            int shift = 0;    // Bit shift amount (increasing by 7 per byte)
        
            while (true)
            {
                // Sanity check: can't shift more than 64 bits into a ulong
                if (shift >= 64)
                {
                    throw new FormatException("ULEB128 sequence is too long for a 64-bit integer.");
                }
        
                // Ensure we don't read past the end of the stream
                if (reader.BaseStream.Position >= reader.BaseStream.Length)
                {
                    throw new EndOfStreamException("Unexpected end of stream while reading ULEB128.");
                }
        
                // Read next byte from the stream
                byte b = reader.ReadByte();
        
                // Mask off the high bit (continuation flag), use lower 7 bits as part of the value
                ulong chunk = (ulong)(b & 0x7F);
        
                // Combine the chunk into the result, shifted to the appropriate bit position
                result |= chunk << shift;
        
                // If the high bit is not set, this was the final byte of the value
                if ((b & 0x80) == 0)
                {
                    break;
                }
        
                // Otherwise, prepare to shift further for the next byte
                shift += 7;
            }
        
            return result;
        }

        private static string GetEncodingName(byte code)
        {
            if (code == 0x01) return "ANSI";
            if (code == 0x02) return "UTF-16LE";
            if (code == 0x03) return "UTF-16BE";
            if (code == 0x04) return "UTF-8 with BOM";
            if (code == 0x05) return "UTF-8";
            return "Unknown";
        }

    }
}

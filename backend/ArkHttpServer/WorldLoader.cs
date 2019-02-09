﻿using ArkHttpServer.Entities;
using ArkSaveEditor.Deserializer.DotArk;
using ArkSaveEditor.World;
using ArkSaveEditor.World.WorldTypes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ArkHttpServer
{
    public delegate void OnMapReloadEvent(ArkWorld world, DateTime time);
    public static class WorldLoader
    {
        private static ArkWorld current_world;
        private static DateTime current_world_time;
        private static bool is_current_load_loaded = false;

        private static DateTime last_check_world_time; //Used by CheckForMapUpdates. Set to the time of the last check.
        private static bool has_last_checked_world_time = false;

        private static Dictionary<string, ArkItemSearchResultsItem> item_dict_cache;
        private static bool item_dict_cache_created = false;

        private static string world_path
        {
            get
            {
                return ArkWebServer.config.save_location;
            }
        }

        //API
        public static ArkWorld GetWorld(out DateTime time)
        {
            //If the world is loaded, return it. Else, reload it
            time = DateTime.MinValue;
            if (is_current_load_loaded)
            {
                time = current_world_time;
                return current_world;
            }

            //Load new world
            LoadArkWorldIntoSlot();
            time = current_world_time;
            return current_world;
        }

        public static ArkWorld GetWorld()
        {
            return GetWorld(out DateTime time);
        }

        public static Dictionary<string, ArkItemSearchResultsItem> GetItemDictForTribe(int tribeId)
        {
            return item_dict_cache;
        }

        /// <summary>
        /// Checks if the map file has been updated. If it has, returns true. Else, returns false.
        /// </summary>
        /// <returns></returns>
        public static bool CheckForMapUpdates()
        {
            //If the map is not loaded, return true.
            if (!is_current_load_loaded)
                return true;

            //If we have never checked, check time and return false
            if(!has_last_checked_world_time)
            {
                last_check_world_time = GetLastWorldEditTime();
                has_last_checked_world_time = true;
                return false;
            }

            //Now, compare the last edit time of the current file to that of the old file.
            DateTime nowFileTime = GetLastWorldEditTime();
            bool status = nowFileTime.Ticks != last_check_world_time.Ticks;

            //Update the time
            last_check_world_time = nowFileTime;

            //Return status
            return status;
        }

        //Private
        private static void LoadArkWorldIntoSlot()
        {
            //Class used to deserialize this
            DotArkDeserializer deser = new DotArkDeserializer();

            //Set the current world time
            DateTime world_time = GetLastWorldEditTime();

            //Load file into memory
            byte[] world_data = File.ReadAllBytes(world_path);

            //Open MemoryStream and read
            using(MemoryStream ms = new MemoryStream(world_data))
            {
                //Rewind
                ms.Position = 0;

                //Read
                deser.OpenArkFile(ms);
            }

            //Get world
            ArkWorld world = new ArkWorld(deser.ark);

            //Compute item dict
            Dictionary<string, ArkItemSearchResultsItem> itemDict = ComputeItemDictCache(world, world.dinos);

            //Set values
            current_world = world;
            item_dict_cache = itemDict;
            current_world_time = world_time;
            is_current_load_loaded = true;
        }

        public static Dictionary<string, ArkItemSearchResultsItem> ComputeItemDictCache(ArkWorld world, List<ArkDinosaur> dinos)
        {
            Dictionary<string, ArkItemSearchResultsItem> itemDict = new Dictionary<string, ArkItemSearchResultsItem>(); //Key: Item classname
            foreach (var d in dinos)
            {
                //Add tuple with inventory item and dino ID
                string dinoId = d.dinosaurId.ToString();
                List<ArkPrimalItem> dino_inventory;
                try
                {
                    dino_inventory = d.GetInventoryItems();
                }
                catch
                {
                    //Skip dino.
                    continue;
                }

                //There will be duplicate values in this, but that is OK.
                foreach (var i in dino_inventory)
                {
                    string classname = i.classnameString;
                    if (i.isEngram)
                        continue;

                    //If the item dict does not contain this item, also add an entry
                    if (!itemDict.ContainsKey(classname))
                    {
                        itemDict.Add(classname, new ArkItemSearchResultsItem
                        {
                            classname = classname,
                            entry = ArkSaveEditor.ArkImports.GetItemDataByClassname(classname),
                            owner_ids = new Dictionary<string, int>(),
                            total_count = 0
                        });
                    }

                    //Set values in the dict
                    itemDict[classname].total_count += i.stackSize;
                    if (!itemDict[classname].owner_ids.ContainsKey(dinoId))
                    {
                        //Does not contain this dino. Add it
                        itemDict[classname].owner_ids.Add(dinoId, i.stackSize);

                        //Also add this dino's data
                        itemDict[classname].owner_dinos.Add(dinoId, new BasicArkDino(d, world));
                    }
                    else
                        //Does contain our dino. Add this stack
                        itemDict[classname].owner_ids[dinoId] += i.stackSize;
                }
            }
            return itemDict;
        }

        private static DateTime GetLastWorldEditTime()
        {
            return File.GetLastWriteTimeUtc(world_path);
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WannaDoWidget
{
    public class DataManager
    {
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WannaDoWidget"
        );
        private static readonly string FilePath = Path.Combine(AppDataFolder, "items.json");

        public List<WannaDoItem> Items { get; private set; } = new List<WannaDoItem>();

        public event EventHandler? DataUpdated;

        public DataManager()
        {
            Load();
        }

        public void Load()
        {
            try
            {
                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                }

                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    var list = JsonSerializer.Deserialize<List<WannaDoItem>>(json);
                    if (list != null)
                    {
                        Items = list;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load data: {ex.Message}");
            }

            CheckAllExpirations(false);
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                }

                string json = JsonSerializer.Serialize(Items, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save data: {ex.Message}");
            }
        }

        public void AddItem(string memo, DateTime? dueDate, bool dueTimeSpecified = false)
        {
            var item = new WannaDoItem
            {
                Memo = memo,
                DueDate = dueDate,
                DueTimeSpecified = dueTimeSpecified,
                State = WannaDoState.Todo
            };
            Items.Add(item);
            Save();
            DataUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateItemState(string id, WannaDoState newState)
        {
            var item = Items.Find(i => i.Id == id);
            if (item != null)
            {
                item.State = newState;
                Save();
                DataUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public void DeleteItem(string id)
        {
            var item = Items.Find(i => i.Id == id);
            if (item != null)
            {
                Items.Remove(item);
                Save();
                DataUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool CheckAllExpirations(bool notifyChange = true)
        {
            bool changed = false;
            foreach (var item in Items)
            {
                if (item.CheckExpiration())
                {
                    changed = true;
                }
            }

            if (changed)
            {
                Save();
                if (notifyChange)
                {
                    DataUpdated?.Invoke(this, EventArgs.Empty);
                }
            }
            return changed;
        }
    }
}

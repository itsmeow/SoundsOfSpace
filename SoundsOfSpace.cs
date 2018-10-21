using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SoundsOfSpace {

    [RequireComponent(typeof(AudioSource))]
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class SoundsOfSpace : UnityEngine.MonoBehaviour {

        private Dictionary<string, UnityEngine.AudioClip> planetMap = new Dictionary<string, UnityEngine.AudioClip>();
        private Dictionary<AudioClip, string> fileNames = new Dictionary<AudioClip, string>();

        private GameObject sourceObject;
        private AudioSource source;

        IEnumerator<int> LoadSounds() {
            string pluginDir = AssemblyLoader.loadedAssemblies.GetPathByType(typeof(SoundsOfSpace));
            string sounds_root = pluginDir.Substring(0, pluginDir.IndexOf("SoundsOfSpace") + 13) + "/Sounds/SoundsOfSpace/";
            sounds_root = sounds_root.Replace('\\', '/');
            Debug.Log("[SoundsOfSpace]  " + "Sound directory: " + sounds_root);
            if(Directory.Exists(sounds_root)) {
                string[] strArray = Directory.GetFiles(sounds_root, "*.ogg");
                foreach(string file in strArray) {
                    Debug.Log("[SoundsOfSpace] " + "Loading file: " + file);
                    int len = sounds_root.Length;
                    string filename = file.Substring(len);
                    int dotpos = filename.LastIndexOf(".");

                    string name = filename.Substring(0, dotpos);

                    string gdb_path = "SoundsOfSpace/Sounds/SoundsOfSpace/" + name;
                    if(GameDatabase.Instance.ExistsAudioClip(gdb_path)) {
                        AudioClip clip = GameDatabase.Instance.GetAudioClip(gdb_path);
                        //clip.name = name; DO NOT DO THIS! Causes fail on second load.
                        planetMap.Add(name, clip);
                        fileNames.Add(clip, name);
                        Debug.Log("[SoundsOfSpace] " + "Clip " + clip.name + " loaded OK");
                    } else {
                        Debug.LogError("[SoundsOfSpace] " + "Failed to load file through database: " + gdb_path);
                        /*
                        // Try again with www method.

                        WWW www = new WWW("file://" + sounds_root + name + ".ogg");
                        while(!www.isDone) {
                            yield return 1;
                        }
                        if(www.GetAudioClip() != null) {
                            AudioClip clip2 = www.GetAudioClip(false, false);
                            clip2.LoadAudioData();
                            clip2.name = name;
                            planetMap.Add(name, clip2);
                            fileNames.Add(clip2, name);
                            Debug.Log("[SoundsOfSpace] " + "Clip " + clip2.name + " loaded OK");
                        } else {
                            Debug.LogError("[SoundsOfSpace] " + "Failed to load " + gdb_path + " with secondary method.");
                        }*/
                    }
                }

            }

            yield return 1;
        }

        public void Start() {

            // Load sounds into dictionary

            StartCoroutine(LoadSounds());

            sourceObject = new GameObject();
            sourceObject.name = "SOS Audio Player Object";
            source = sourceObject.AddComponent<AudioSource>();
            source.name = "SOS Audio Source";
            source.spatialBlend = 0.0f;
            source.spatialize = false;
            source.loop = true;
            source.volume = 1.3f;
            source.mute = false;
            source.enabled = true;

            Debug.Log("[SoundsOfSpace] " + "Sound player created.");

            GameEvents.onVesselSOIChanged.Add(OnVesselSOIChanged);
            GameEvents.onVesselChange.Add(OnVesselChange);
            GameEvents.onVesselSituationChange.Add(OnVesselSituationChange);
            Debug.Log("[SoundsOfSpace] " + "Events registered.");

            if(FlightGlobals.ActiveVessel != null) {
                OnVesselChange(FlightGlobals.ActiveVessel);
            }
        }

        void playClipFor(Vessel vessel, CelestialBody body) {
            if(source.isPlaying) {
                Debug.Log("[SoundsOfSpace] " + "Audio playing already. Stopping.");
                source.Stop();
            }
            String planetName = body.GetName();
            AudioClip clipForBody = null;
            bool foundClip = planetMap.TryGetValue(planetName, out clipForBody);
            if(foundClip) {
                Debug.Log("[SoundsOfSpace] " + "Play clip for " + planetName + ": " + clipForBody);
                source.clip = clipForBody;
                sourceObject.SetActive(true);
                source.Play();
            } else {
                Debug.LogWarning("[SoundsOfSpace] " + "Did not find audio clip for body: " + planetName);
                if(source.isPlaying) {
                    source.Stop();
                }
            }
        }

        void stopPlaying() {
            if(source.isPlaying) {
                source.Stop();
            }
        }

        void OnVesselSOIChanged(GameEvents.HostedFromToAction<Vessel, CelestialBody> data) {
            if(data.host.isActiveVessel) {
                Debug.Log("[SoundsOfSpace] " + "SOI Changed detected. Changing sounds.");
                if(data.host.situation == Vessel.Situations.ORBITING || data.host.situation == Vessel.Situations.ESCAPING || data.host.situation == Vessel.Situations.DOCKED) {
                    playClipFor(data.host, data.to);
                } else {
                    Debug.Log("[SoundsOfSpace] " + "Canceled due to non orbital trajectory.");
                    stopPlaying();
                }
            }
        }

        void OnVesselChange(Vessel data) {
            if(FlightGlobals.ActiveVessel != null) {
                Debug.Log("[SoundsOfSpace] " + "Vessel changed, getting new sounds.");
                if(data.situation == Vessel.Situations.ORBITING || data.situation == Vessel.Situations.ESCAPING || data.situation == Vessel.Situations.DOCKED) {
                    playClipFor(data, data.mainBody);
                } else {
                    Debug.Log("[SoundsOfSpace] " + "Canceled due to non orbital trajectory.");
                    stopPlaying();
                }
            }
        }

        void OnVesselSituationChange(GameEvents.HostedFromToAction<Vessel, Vessel.Situations> data) {
            if(FlightGlobals.ActiveVessel != null && data.host.isActiveVessel) {
                if(source.isPlaying) {
                    if(!(data.host.situation == Vessel.Situations.ORBITING || data.host.situation == Vessel.Situations.ESCAPING || data.host.situation == Vessel.Situations.DOCKED)) {
                        Debug.Log("[SoundsOfSpace] " + "Vessel has landed or gone suborbital. Stopping Sound.");
                        stopPlaying();
                    }
                } else {
                    if(data.host.situation == Vessel.Situations.ORBITING || data.host.situation == Vessel.Situations.ESCAPING || data.host.situation == Vessel.Situations.DOCKED) {
                        OnVesselChange(data.host); // Update sounds
                    }
                }
            }
        }

        internal void OnDestroy() {
            GameEvents.onVesselSOIChanged.Remove(OnVesselSOIChanged);
            GameEvents.onVesselChange.Remove(OnVesselChange);
            GameEvents.onVesselSituationChange.Remove(OnVesselSituationChange);
            Debug.Log("[SoundsOfSpace] " + "Events unregistered.");
        }

    }
}

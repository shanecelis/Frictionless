using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// A simple, *single-threaded*, service locator appropriate for use with Unity.
/// </summary>

namespace Frictionless
{
	public static class ServiceFactory
	{
		private static readonly Dictionary<Type,Type> singletons = new Dictionary<Type, Type>();
		private static readonly Dictionary<Type,Type> transients = new Dictionary<Type, Type>();
		private static readonly Dictionary<Type,object> singletonInstances = new Dictionary<Type, object>();

        /// Call Reset if Domain Reloading is enabled. Otherwise the state will carry over
        /// from run to run in the editor.
        // https://blogs.unity3d.com/2019/11/05/enter-play-mode-faster-in-unity-2019-3/
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init() {
            Reset();
        }

		public static bool IsEmpty
		{
			get { return singletons.Count == 0 && transients.Count == 0; }
		}

		public static void Reset()
		{
			List<Type> survivorRegisteredTypes = new List<Type>();
			List<object> survivors = new List<object>();
			foreach(KeyValuePair<Type,object> pair in singletonInstances)
			{
				if (pair.Value is IMultiSceneSingleton)
				{
					survivors.Add(pair.Value);
					survivorRegisteredTypes.Add(pair.Key);
				}
			}
			singletons.Clear();
			transients.Clear();
			singletonInstances.Clear();

			for (int i = 0; i < survivors.Count; i++)
			{
				singletonInstances[survivorRegisteredTypes[i]] = survivors[i];
				singletons[survivorRegisteredTypes[i]] = survivors[i].GetType();
			}
		}

		public static void RegisterSingleton<TConcrete>()
		{
			singletons[typeof(TConcrete)] = typeof(TConcrete);
		}

		public static void RegisterSingleton<TAbstract,TConcrete>()
		{
			singletons[typeof(TAbstract)] = typeof(TConcrete);
		}
		
		public static void RegisterSingleton<TConcrete>(TConcrete instance, bool onlyIfNotExists = false) where TConcrete : class
		{
			if (onlyIfNotExists && Resolve<TConcrete>() != default)
			{
				return;
			}
			singletons[typeof(TConcrete)] = typeof(TConcrete);
			singletonInstances[typeof(TConcrete)] = instance;
		}

        public static void UnregisterSingleton<TConcrete>(TConcrete instance)
        {
            if (singletonInstances.ContainsKey(typeof(TConcrete)))
            {
                singletonInstances.Remove(typeof(TConcrete));
            }
        }

		public static void RegisterTransient<TAbstract,TConcrete>()
		{
			transients[typeof(TAbstract)] = typeof(TConcrete);
		}

		public static T Resolve<T>() where T : class
		{
			return Resolve<T>(false);
		}

		public static T Resolve<T>(bool onlyExisting) where T : class
		{
			T result = default(T);
			Type concreteType = null;
			if (singletons.TryGetValue(typeof(T), out concreteType))
			{
				object r = null;
				if (!singletonInstances.TryGetValue(typeof(T), out r) && !onlyExisting)
				{
	#if NETFX_CORE
					if (concreteType.GetTypeInfo().IsSubclassOf(typeof(MonoBehaviour)))
	#else
					if (concreteType.IsSubclassOf(typeof(MonoBehaviour)))
	#endif
					{
						GameObject singletonGameObject = new GameObject();
						r = singletonGameObject.AddComponent(concreteType);
						singletonGameObject.name = $"{typeof(T)} (singleton)";
					}
					else
						r = Activator.CreateInstance(concreteType);
					singletonInstances[typeof(T)] = r;
				}
				result = (T)r;
			}
			else if (transients.TryGetValue(typeof(T), out concreteType))
			{
				object r = Activator.CreateInstance(concreteType);
				result = (T)r;
			}
			return result;
		}

		public static IEnumerator HandleSceneLoad(AsyncOperation sceneLoadOperation)
		{
			yield return sceneLoadOperation;
			HandleSceneLoaded();
		}

		public static void HandleSceneLoaded()
		{
			foreach(KeyValuePair<Type,object> pair in singletonInstances)
			{
				if (pair.Value is IReinitializingMultiSceneSingleton reinitializingMultiSceneSingleton)
				{
					reinitializingMultiSceneSingleton.ReinitializeAfterSceneLoad();
				}
			}
		}
	}
}

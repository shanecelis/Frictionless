﻿using UnityEngine;
using System.Collections;
using Frictionless;

public class GameConfig : MonoBehaviour
{
	void Awake()
	{
		ServiceFactory.Instance.RegisterSingleton<MessageRouter>();
	}
}

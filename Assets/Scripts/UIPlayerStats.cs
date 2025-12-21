using UnityEngine;
using TMPro;

public class UIPlayerStats : MonoBehaviour
{
	[SerializeField] private TextMeshProUGUI lengthText;

	private void OnEnable()
	{
		PlayerLenght.ChangedLengthEvent += ChangeLengthText;
	}

	private void OnDisable()
	{
		PlayerLenght.ChangedLengthEvent -= ChangeLengthText;
	}

	private void ChangeLengthText(ushort length)
	{
		lengthText.text = length.ToString();
	}
}

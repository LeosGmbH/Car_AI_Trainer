using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static MLAgentController;

public class TrackCheckpoints : MonoBehaviour
{
    //private List<SingleCheckpoint> checkPointSingleList;
    //private int nextCheckpointSingleIndex;
    //public MLAgentController parkourAgent;
    //private void Awake()
    //{
    //    CollectSingleCheckpoints();
    //}
    //public void CollectSingleCheckpoints()
    //{
    //    checkPointSingleList = new List<SingleCheckpoint>();
    //    foreach (Transform checkPointSingleTransform in gameObject.transform)
    //    {
    //        SingleCheckpoint checkPointSingle = checkPointSingleTransform.GetComponent<SingleCheckpoint>();
    //        checkPointSingle.SetTrackChechpoints(this);
    //        checkPointSingleList.Add(checkPointSingle);
    //        checkPointSingleTransform.gameObject.SetActive(false);
    //    }
    //    nextCheckpointSingleIndex = 0;
    //    checkPointSingleList[nextCheckpointSingleIndex].gameObject.SetActive(true);
    //}
    //public void PlayerThroughCheckpoint(SingleCheckpoint checkpointSingle)
    //{
    //    int currentIndex = checkPointSingleList.IndexOf(checkpointSingle);
    //    if (currentIndex == nextCheckpointSingleIndex)
    //    {
    //        nextCheckpointSingleIndex = (nextCheckpointSingleIndex + 1) % checkPointSingleList.Count;
    //        if (checkpointSingle.gameObject.name != "BtnPress") //für Lvl 3
    //        {
    //            checkpointSingle.gameObject.SetActive(false);
    //        }

    //        float rewardPerCheckpoint = 50f / checkPointSingleList.Count;
    //        checkPointSingleList[nextCheckpointSingleIndex].gameObject.SetActive(true);

    //        // Prüfen, ob es das letzte Objekt ist
    //        if (currentIndex == checkPointSingleList.Count - 1)
    //        {
    //            parkourAgent.ReachGoal();
    //        }
    //    }
    //}
    //public void ResetCheckPoints()
    //{
    //    nextCheckpointSingleIndex = 0;
    //    foreach (Transform checkPointSingleTransform in gameObject.transform)
    //    {
    //        checkPointSingleTransform.gameObject.SetActive(false);
    //    }
    //    checkPointSingleList[nextCheckpointSingleIndex].gameObject.SetActive(true);
    //}

    

}

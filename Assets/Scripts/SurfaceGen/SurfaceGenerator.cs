using System;
using UnityEngine;

public class SurfaceGenerator : MonoBehaviour
{
    [Header("Colors")] public Color surfaceColor;
    public Color outerWallsColor;
    public Color innerWallsColor;

    [Header("Parameters")] public SurfaceParameters parameters;
    public GameObject wallPrefab;

    private RoomFinder finder;

    private void Awake()
    {
        finder = GetComponent<RoomFinder>();

        GenerateSurface(parameters);

        finder.FindRooms();
    }

    void GenerateSurface(SurfaceParameters localParameters)
    {
        GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Plane);
        surface.transform.parent = transform;
        surface.transform.localScale = localParameters.scale / 10;
        surface.GetComponent<Renderer>().material.color = surfaceColor;
        finder.floor = surface.GetComponent<Renderer>();

        for (int i = 0; i < 4; i++)
        {
            GameObject outerWall = Instantiate(wallPrefab, transform, true);
            outerWall.name = "outerWall" + i;

            Vector2[] pos =
            {
                new(0, localParameters.scale.z / 2),
                new(localParameters.scale.x / 2, 0),
                new(0, -(localParameters.scale.z / 2)),
                new(-(localParameters.scale.x / 2), 0)
            };

            outerWall.transform.localScale = new Vector3(
                i % 2 == 0 ? localParameters.scale.x : localParameters.scale.z,
                localParameters.scale.y,
                1
            );
            outerWall.transform.position = new Vector3(
                pos[i].x,
                localParameters.scale.y / 2,
                pos[i].y
            );
            outerWall.transform.Rotate(0, i % 2 != 0 ? 90f : 0, 0);

            outerWall.GetComponent<WallRender>().wallColor = outerWallsColor;
        }

        foreach (var wall in localParameters.wallParameters)
        {
            GameObject innerWall = Instantiate(wallPrefab, transform, true);
            innerWall.GetComponent<WallRender>().wallColor = innerWallsColor;

            innerWall.transform.position = new Vector3(
                wall.startPoint.x / 2,
                localParameters.scale.y / 2,
                wall.startPoint.y / 2
            );
            innerWall.transform.localScale = new Vector3(
                wall.startPoint.x - wall.endPoint.x + (wall.startPoint.x - wall.endPoint.x) == 0
                    ? 1
                    : wall.startPoint.x - wall.endPoint.x,
                localParameters.scale.y,
                wall.startPoint.y - wall.endPoint.y + (wall.startPoint.y - wall.endPoint.y) == 0
                    ? 1
                    : wall.startPoint.y - wall.endPoint.y
            );
            if (innerWall.transform.localScale.z != 0)
            {
                innerWall.transform.localScale =
                    new Vector3(innerWall.transform.localScale.z, innerWall.transform.localScale.y, 1);
                innerWall.transform.Rotate(innerWall.transform.localRotation.eulerAngles.x, 90f,
                    innerWall.transform.localRotation.eulerAngles.z);
            }
        }
    }
}
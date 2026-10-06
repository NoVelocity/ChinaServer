using System.Collections.Generic;
using Newtonsoft.Json;

namespace SurfaceGen
{
    public class CamerasConfiguration
    {
        public List<RoomConfiguration> rooms = new();
        public List<RobotConfiguration> robots = new();

        public CamerasConfiguration(RoomParameters[] rooms, RobotParameters[] robots)
        {
            foreach (var room in rooms)
            {
                this.rooms.Add(new RoomConfiguration(room));
            }

            foreach (var robot in robots)
            {
                this.robots.Add(new RobotConfiguration(robot));
            }
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    public class RoomConfiguration
    {
        public string id;
        public float w, h, d;
        public int[] markers;

        public RoomConfiguration(RoomParameters room)
        {
            id = room.id;

            w = room.size.x;
            h = room.size.y;
            d = room.size.z;

            markers = room.markers;
        }
    }

    public class RobotConfiguration
    {
        public string id;
        public int n, s, e, w;

        public RobotConfiguration(RobotParameters robot)
        {
            id = robot.id;

            n = robot.northMarker;
            e = robot.eastMarker;
            w = robot.westMarker;
            s = robot.southMarker;
        }
    }
}
# Practice Before The Patient

A .NET 9 Blazor application for medical training simulations, consisting of a Web frontend and an API backend.

##  Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Windows, Mac, or Linux)
- Ensure Docker Desktop is running before starting the application

##  Getting Started

### Using Visual Studio

1. **Open the solution** in Visual Studio 2022
2. **Select the Docker Compose profile**:
   - In the toolbar, change the startup profile dropdown to **"Docker Compose"**
3. **Start debugging**:
   - Press `F5` or click the Start button
   - Visual Studio will automatically build images and start containers

### Using Command Line

1. **Navigate to the solution directory**:
   ```bash
   cd CS495-PracticeBeforeThePatient
   ```

2. **Build and start the containers**:
   ```bash
   docker-compose up --build
   ```
   
   Or run in detached mode (background):
   ```bash
   docker-compose up -d --build
   ```

3. **View logs** (if running in detached mode):
   ```bash
   docker-compose logs -f
   ```

4. **Access the application**:
   - Web UI: `http://localhost:5009` or `https://localhost:7124`
   - API: `http://localhost:5186` or `https://localhost:7144`

5. **Stop the containers**:
   ```bash
   docker-compose down
   ```

##  Troubleshooting

### Docker Containers Won't Start
- Ensure Docker Desktop is running
- Check Docker logs: `docker-compose logs`
- Try rebuilding: `docker-compose down && docker-compose up --build`
- Verify no port conflicts on host machine

### API Connection Issues
- Verify both containers are running: `docker-compose ps`
- Check API logs: `docker-compose logs practicebeforethepatient.api`
- Check Web logs: `docker-compose logs practicebeforethepatient.web`

### Port Already in Use
- Stop any running instances: `docker-compose down`
- Check for conflicting processes using ports 5009, 5186, 7124, or 7144
- Kill conflicting processes or modify ports in `docker-compose.override.yml`


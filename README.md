# PocketStoryPoker

## Project Overview

PocketStoryPoker is a Planning Poker application designed for agile estimation. It allows teams to collaboratively estimate tasks using virtual cards in real-time. The application focuses on simplicity and ease of use, providing a seamless experience for remote teams.

## Features and How to Play

- **Session Creation and Joining**: Users can create new estimation sessions or join existing ones.
- **Card-Based Voting System**: Participants vote using cards with values 1, 2, 3, 5, 8, and 13.
- **Real-Time Vote Tracking**: Votes are tracked in real-time, and participants can see the current state of the estimation.
- **Results View**: After voting, the results are displayed with statistics such as median, average, and a recommended value.
- **Responsive Design**: The application is designed to work on all devices, ensuring a smooth experience on desktops, tablets, and mobile phones.

## Development Approach

This project was developed as an experiment using pure natural language requirements passed to Cline, an AI agent. The development process involved near-zero code interventions and no judgments on how the requirements were met. For more information on Cline, refer to the [Cline documentation](https://example.com/cline-docs).

## Tech Stack

- **.NET 9.0**: The backend is built using .NET 9.0.
- **Blazor**: The frontend is developed using Blazor.
- **SignalR**: Real-time communication is handled using SignalR.
- **Docker**: The application can be containerized and run using Docker.

## Local Environment Setup

### Prerequisites

- .NET 9.0 SDK

### Steps

1. Clone the repository:

   ```sh
   git clone https://github.com/yourusername/PocketStoryPoker.git
   cd PocketStoryPoker
   ```

2. Build the project:

   ```sh
   dotnet build
   ```

3. Run the application:

   ```sh
   dotnet run --project src/PocketStoryPoker.csproj
   ```

## Docker Setup

### Build Docker Image

```sh
docker build -t pocketstorypoker .
```

### Run Docker Container

```sh
docker run -d -p 80:80 pocketstorypoker
```

### Environment Configuration

The application exposes port 80 by default. You can configure the environment variables as needed in the Dockerfile or during the `docker run` command.

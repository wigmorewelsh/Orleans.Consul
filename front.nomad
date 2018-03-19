# There can only be a single job definition per file. This job is named
# "example" so it will create a job with the ID and Name "example".

job "frontend" {

  datacenters = ["dc1"]

  type = "service"

  update {
    max_parallel = 1
    min_healthy_time = "10s"
    healthy_deadline = "3m"
    auto_revert = false
    canary = 0
  }

  group "cache" {
    count = 3

    restart {
      attempts = 10
      interval = "5m"

      delay = "25s"

      mode = "delay"
    }

    task "frontend" {
      # The "driver" parameter specifies the task driver that should be used to
      # run the task.
      driver = "raw_exec"

      # The "config" stanza specifies the driver configuration, which is passed
      # directly to the driver to start the task. The details of configurations
      # are specific to each driver, so please see specific driver
      # documentation for more information.
      config {
        command  = "front.exe"
        args = [
          "--Port", "${NOMAD_PORT_http}"
        ]
      }

      artifact {
        source = "s3::http://127.0.0.1:9000/nomad/front.18193128.zip?archive=zip"
        options {
          aws_access_key_id     = "XOS68RHLTHGF30GTE5SM"
          aws_access_key_secret = "MNAspSp1/kcQq+sBxnk1ygIZhuwmk/JhibMzu/AO"
        }
      }

      resources {
        cpu    = 500 # 500 MHz
        memory = 256 # 256MB
        network {
          mbits = 10
          port "http" {}
        }
      }

      logs {
        max_files     = 10
        max_file_size = 10
      }

      service {
        name = "global-front-check"
        tags = ["global", "cache", "urlprefix-/test strip=/test"]

        port = "http"

        check {
          type = "http"
          port = "http"
          name = "front-check"
          path = "/api/values"
          interval = "5s"
          timeout  = "2s"
        }
      }
    }
  }
}
